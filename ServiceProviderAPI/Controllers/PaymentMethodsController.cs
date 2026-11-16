using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/payment-methods")]
[Authorize]
public class PaymentMethodsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PaymentMethodsController(ApplicationDbContext context) => _context = context;

    private int CallerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsUser => User.IsInRole("User");
    private bool IsPro => User.IsInRole("Pro");

    // ── GET /api/payment-methods ──────────────────────────────────────────
    // Returns all payment methods for the caller.
    // Pros may pass ?businessId=X to list a business's methods (must be a member).

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] int? businessId = null)
    {
        IQueryable<PaymentMethod> query;

        if (businessId.HasValue && IsPro)
        {
            var isMember = await _context.ProBusinessMemberships.AnyAsync(
                m => m.BusinessId == businessId && m.ProId == CallerId && m.Status == "Active");
            if (!isMember) return Forbid();
            query = _context.PaymentMethods.Where(pm => pm.BusinessId == businessId);
        }
        else if (IsPro)
            query = _context.PaymentMethods.Where(pm => pm.ProId == CallerId);
        else
            query = _context.PaymentMethods.Where(pm => pm.UserId == CallerId);

        var methods = await query.OrderByDescending(pm => pm.IsDefault).ThenBy(pm => pm.CreatedAt)
            .Select(pm => MapToDto(pm)).ToListAsync();

        return Ok(methods);
    }

    // ── GET /api/payment-methods/checkout-context ─────────────────────────
    // User only — returns their payment methods + saved address for billing.

    [HttpGet("checkout-context")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetCheckoutContext()
    {
        var userId = CallerId;

        var paymentMethods = await _context.PaymentMethods
            .Where(pm => pm.UserId == userId)
            .OrderByDescending(pm => pm.IsDefault).ThenBy(pm => pm.CreatedAt)
            .Select(pm => MapToDto(pm))
            .ToListAsync();

        var address = await _context.Users
            .Where(u => u.Id == userId)
            .Include(u => u.Address)
            .Select(u => u.Address == null ? null : new
            {
                u.Address.Id,
                u.Address.HouseNameNumber,
                u.Address.Street1,
                u.Address.Street2,
                u.Address.City,
                u.Address.District,
                u.Address.State,
                u.Address.Country,
                u.Address.ZipPostalCode
            })
            .FirstOrDefaultAsync();

        return Ok(new { paymentMethods, billingAddress = address });
    }

    // ── POST /api/payment-methods ─────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PaymentMethodRequest req)
    {
        var (ownerId, ownerType, error) = await ResolveOwner(req.BusinessId);
        if (error != null) return error;

        if (ValidateTypeForOwner(req, ownerType) is { } typeError)
            return typeError;

        // If this is marked as default, clear other defaults for same owner
        if (req.IsDefault ?? false)
            await ClearDefaults(ownerId, ownerType);

        var isUpi = req.Type == "UPI";
        var pm = new PaymentMethod
        {
            UserId = ownerType == "User" ? ownerId : null,
            ProId = ownerType == "Pro" ? ownerId : null,
            BusinessId = ownerType == "Business" ? req.BusinessId : null,
            Type = req.Type,
            Label = req.Label?.Trim(),
            IsDefault = req.IsDefault ?? false,
            // Store only the fields relevant to the chosen type — a UPI method never retains bank details.
            UpiVpa = isUpi ? req.UpiVpa?.Trim() : null,
            BankAccountHolderName = isUpi ? null : req.BankAccountHolderName?.Trim(),
            BankAccountNumber = isUpi ? null : req.BankAccountNumber?.Trim(),
            BankIfsc = isUpi ? null : req.BankIfsc?.Trim().ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow
        };

        _context.PaymentMethods.Add(pm);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMine), MapToDto(pm));
    }

    // ── PUT /api/payment-methods/{id} ─────────────────────────────────────

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] PaymentMethodRequest req)
    {
        var pm = await _context.PaymentMethods.FindAsync(id);
        if (pm == null) return NotFound();
        if (!OwnsMethod(pm)) return Forbid();

        // A method owned by a consumer stays UPI-only, matching the create-time rule.
        var ownerType = pm.UserId.HasValue ? "User" : pm.ProId.HasValue ? "Pro" : "Business";
        if (ValidateTypeForOwner(req, ownerType) is { } typeError)
            return typeError;

        var isUpi = req.Type == "UPI";
        bool detailsChanged = pm.Type != req.Type
            || pm.UpiVpa != (isUpi ? req.UpiVpa?.Trim() : null)
            || pm.BankAccountNumber != (isUpi ? null : req.BankAccountNumber?.Trim())
            || pm.BankIfsc != (isUpi ? null : req.BankIfsc?.Trim().ToUpperInvariant());

        pm.Type = req.Type;
        pm.Label = req.Label?.Trim();
        pm.UpiVpa = isUpi ? req.UpiVpa?.Trim() : null;
        pm.BankAccountHolderName = isUpi ? null : req.BankAccountHolderName?.Trim();
        pm.BankAccountNumber = isUpi ? null : req.BankAccountNumber?.Trim();
        pm.BankIfsc = isUpi ? null : req.BankIfsc?.Trim().ToUpperInvariant();

        // Reset Razorpay infra if payout details changed
        if (detailsChanged) pm.RazorpayFundAccountId = null;

        await _context.SaveChangesAsync();
        return Ok(MapToDto(pm));
    }

    // ── DELETE /api/payment-methods/{id} ──────────────────────────────────

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var pm = await _context.PaymentMethods.FindAsync(id);
        if (pm == null) return NotFound();
        if (!OwnsMethod(pm)) return Forbid();

        _context.PaymentMethods.Remove(pm);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── PUT /api/payment-methods/{id}/set-default ──────────────────────────

    [HttpPut("{id}/set-default")]
    public async Task<IActionResult> SetDefault(int id)
    {
        var pm = await _context.PaymentMethods.FindAsync(id);
        if (pm == null) return NotFound();
        if (!OwnsMethod(pm)) return Forbid();

        var (ownerId, ownerType, _) = ResolveOwnerFromMethod(pm);
        await ClearDefaults(ownerId, ownerType);

        pm.IsDefault = true;
        await _context.SaveChangesAsync();
        return Ok(MapToDto(pm));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    // Validates the method type and enforces the consumer/payout split:
    // consumers may save only a UPI handle; Bank details exist solely for Pro/Business payouts.
    private BadRequestObjectResult? ValidateTypeForOwner(PaymentMethodRequest req, string ownerType)
    {
        if (req.Type != "UPI" && req.Type != "Bank")
            return BadRequest(new { message = "Type must be 'UPI' or 'Bank'" });

        if (ownerType == "User" && req.Type != "UPI")
            return BadRequest(new { message = "Only UPI methods can be saved on a customer account." });

        if (req.Type == "UPI" && string.IsNullOrWhiteSpace(req.UpiVpa))
            return BadRequest(new { message = "UpiVpa is required for UPI type" });

        if (req.Type == "Bank" && (string.IsNullOrWhiteSpace(req.BankAccountNumber) || string.IsNullOrWhiteSpace(req.BankIfsc)))
            return BadRequest(new { message = "BankAccountNumber and BankIfsc are required for Bank type" });

        return null;
    }

    private bool OwnsMethod(PaymentMethod pm)
    {
        if (IsUser && pm.UserId == CallerId) return true;
        if (IsPro && pm.ProId == CallerId) return true;
        // Business methods: the calling Pro must be an active member
        if (IsPro && pm.BusinessId.HasValue)
            return _context.ProBusinessMemberships.Any(
                m => m.BusinessId == pm.BusinessId && m.ProId == CallerId && m.Status == "Active");
        return false;
    }

    private async Task<(int ownerId, string ownerType, IActionResult? error)> ResolveOwner(int? businessId)
    {
        if (businessId.HasValue && IsPro)
        {
            var isMember = await _context.ProBusinessMemberships.AnyAsync(
                m => m.BusinessId == businessId && m.ProId == CallerId && m.Status == "Active");
            if (!isMember) return (0, "", Forbid());
            return (businessId.Value, "Business", null);
        }
        if (IsPro) return (CallerId, "Pro", null);
        return (CallerId, "User", null);
    }

    private static (int ownerId, string ownerType, IActionResult? error) ResolveOwnerFromMethod(PaymentMethod pm)
    {
        if (pm.UserId.HasValue) return (pm.UserId.Value, "User", null);
        if (pm.ProId.HasValue) return (pm.ProId.Value, "Pro", null);
        if (pm.BusinessId.HasValue) return (pm.BusinessId.Value, "Business", null);
        return (0, "", null);
    }

    private async Task ClearDefaults(int ownerId, string ownerType)
    {
        IQueryable<PaymentMethod> q = ownerType switch
        {
            "User" => _context.PaymentMethods.Where(pm => pm.UserId == ownerId && pm.IsDefault),
            "Pro" => _context.PaymentMethods.Where(pm => pm.ProId == ownerId && pm.IsDefault),
            _ => _context.PaymentMethods.Where(pm => pm.BusinessId == ownerId && pm.IsDefault)
        };
        await q.ExecuteUpdateAsync(s => s.SetProperty(pm => pm.IsDefault, false));
    }

    private static object MapToDto(PaymentMethod pm) => new
    {
        pm.Id,
        pm.Type,
        pm.Label,
        pm.IsDefault,
        pm.UpiVpa,
        pm.BankAccountHolderName,
        BankAccountNumber = pm.BankAccountNumber != null
            ? "****" + pm.BankAccountNumber[^Math.Min(4, pm.BankAccountNumber.Length)..]
            : null,
        pm.BankIfsc,
        pm.CreatedAt,
        OwnerType = pm.UserId.HasValue ? "User" : pm.ProId.HasValue ? "Pro" : "Business"
    };
}

// ── Request model ─────────────────────────────────────────────────────────────

public class PaymentMethodRequest
{
    [Required]
    public string Type { get; set; } = null!;

    [StringLength(100)]
    public string? Label { get; set; }

    public bool? IsDefault { get; set; }

    [StringLength(100)]
    public string? UpiVpa { get; set; }

    [StringLength(100)]
    public string? BankAccountHolderName { get; set; }

    [StringLength(50)]
    public string? BankAccountNumber { get; set; }

    [StringLength(20)]
    public string? BankIfsc { get; set; }

    // If set (and caller is a Pro), applies to the business instead of the Pro
    public int? BusinessId { get; set; }
}
