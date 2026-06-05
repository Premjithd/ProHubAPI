using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.DTOs;
using ServiceProviderAPI.Models;
using ServiceProviderAPI.Services;
using BC = BCrypt.Net.BCrypt;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPayoutService _payoutService;

    public ProsController(ApplicationDbContext context, IPayoutService payoutService)
    {
        _context = context;
        _payoutService = payoutService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<object>>> GetPros()
    {
        var pros = await _context.Pros.Include(p => p.Services).ToListAsync();
        return Ok(pros.Select(p => SafePro(p)));
    }

    [HttpGet("browse")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<object>>> BrowsePros(
        [FromQuery] string? search = null,
        [FromQuery] int? categoryId = null)
    {
        var query = _context.Pros.Include(p => p.Services).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(p =>
                p.ProName.ToLower().Contains(term) ||
                p.BusinessName.ToLower().Contains(term) ||
                (p.City != null && p.City.ToLower().Contains(term)) ||
                p.Services.Any(s => s.Name.ToLower().Contains(term)));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.Services.Any(s => s.ServiceCategoryId == categoryId.Value));
        }

        var pros = await query.ToListAsync();

        return Ok(pros.Select(p => new
        {
            p.Id, p.ProName, p.BusinessName,
            p.City, p.State, p.Country,
            p.Latitude, p.Longitude, p.ServiceRadiusKm,
            p.IsEmailVerified,
            Services = p.Services?.Select(s => new { s.Id, s.Name, s.Price })
        }));
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<object>> GetPro(int id)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int.TryParse(callerIdStr, out int callerId);
        bool isAdmin = User.IsInRole("Admin");
        bool isPro = User.IsInRole("Pro");

        // Pros can only view their own full profile; users and admins can view any pro's public profile
        if (isPro && !isAdmin && callerId != id)
            return Forbid();

        var pro = await _context.Pros
            .Include(p => p.Services)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (pro == null) return NotFound();

        return Ok(SafePro(pro));
    }

    private static object SafePro(Pro p) => new
    {
        p.Id, p.ProName, p.BusinessName, p.Email, p.PhoneNumber,
        p.HouseNameNumber, p.Street1, p.Street2, p.City, p.State,
        p.Country, p.ZipPostalCode, p.ServiceRadiusKm,
        p.Latitude, p.Longitude, p.CreatedAt, p.UpdatedAt,
        p.IsEmailVerified, p.IsPhoneVerified,
        Services = p.Services?.Select(s => new { s.Id, s.Name, s.Description, s.Price })
    };

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Pro>> CreatePro(Pro pro)
    {
        pro.PasswordHash = BC.HashPassword(pro.PasswordHash);
        pro.CreatedAt = DateTime.UtcNow;
        pro.UpdatedAt = DateTime.UtcNow;

        _context.Pros.Add(pro);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPro), new { id = pro.Id }, pro);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Pro")]
    public async Task<ActionResult<Pro>> UpdatePro(int id, Pro pro)
    {
        if (id != pro.Id)
        {
            return BadRequest();
        }

        var existingPro = await _context.Pros.FindAsync(id);
        if (existingPro == null)
        {
            return NotFound();
        }

        existingPro.ProName = pro.ProName;

        if (!string.Equals(existingPro.Email, pro.Email, StringComparison.OrdinalIgnoreCase))
        {
            existingPro.Email = pro.Email;
            existingPro.IsEmailVerified = false;
        }

        if (existingPro.PhoneNumber != pro.PhoneNumber)
        {
            existingPro.PhoneNumber = pro.PhoneNumber;
            existingPro.IsPhoneVerified = false;
        }

        if (!string.IsNullOrEmpty(pro.PasswordHash))
        {
            existingPro.PasswordHash = BC.HashPassword(pro.PasswordHash);
        }
        existingPro.BusinessName = pro.BusinessName;
        existingPro.HouseNameNumber = pro.HouseNameNumber;
        existingPro.Street1 = pro.Street1;
        existingPro.Street2 = pro.Street2;
        existingPro.City = pro.City;
        existingPro.State = pro.State;
        existingPro.Country = pro.Country;
        existingPro.ZipPostalCode = pro.ZipPostalCode;
        existingPro.ServiceRadiusKm = pro.ServiceRadiusKm;
        existingPro.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!ProExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return Ok(SafePro(existingPro));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Pro")]
    public async Task<IActionResult> DeletePro(int id)
    {
        var pro = await _context.Pros.FindAsync(id);
        if (pro == null)
        {
            return NotFound();
        }

        _context.Pros.Remove(pro);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // GET: api/pros/{id}/bank-details
    [HttpGet("{id}/bank-details")]
    [Authorize(Roles = "Pro,Admin")]
    public async Task<IActionResult> GetBankDetails(int id)
    {
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        bool isAdmin = User.IsInRole("Admin");

        if (!isAdmin && callerId != id)
            return Forbid();

        var pro = await _context.Pros.FindAsync(id);
        if (pro == null) return NotFound();

        var hasBankDetails = pro.PayoutMethod == "UPI"
            ? !string.IsNullOrWhiteSpace(pro.UpiVpa)
            : !string.IsNullOrWhiteSpace(pro.BankAccountNumber) && !string.IsNullOrWhiteSpace(pro.BankIfsc);

        return Ok(new ProBankDetailsDto
        {
            PayoutMethod = pro.PayoutMethod,
            BankAccountHolderName = pro.BankAccountHolderName,
            BankAccountNumber = pro.BankAccountNumber,
            BankIfsc = pro.BankIfsc,
            UpiVpa = pro.UpiVpa,
            HasBankDetails = hasBankDetails
        });
    }

    // PUT: api/pros/{id}/bank-details
    [HttpPut("{id}/bank-details")]
    [Authorize(Roles = "Pro")]
    public async Task<IActionResult> UpdateBankDetails(int id, [FromBody] UpdateBankDetailsRequest request)
    {
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        if (callerId != id) return Forbid();

        if (request.PayoutMethod != "Bank" && request.PayoutMethod != "UPI")
            return BadRequest(new { message = "PayoutMethod must be 'Bank' or 'UPI'" });

        if (request.PayoutMethod == "Bank" &&
            (string.IsNullOrWhiteSpace(request.BankAccountNumber) || string.IsNullOrWhiteSpace(request.BankIfsc)))
            return BadRequest(new { message = "BankAccountNumber and BankIfsc are required for Bank payout method" });

        if (request.PayoutMethod == "UPI" && string.IsNullOrWhiteSpace(request.UpiVpa))
            return BadRequest(new { message = "UpiVpa is required for UPI payout method" });

        var pro = await _context.Pros.FindAsync(id);
        if (pro == null) return NotFound();

        // If bank details changed, clear cached Razorpay fund account so a new one is created
        bool detailsChanged =
            pro.PayoutMethod != request.PayoutMethod ||
            pro.BankAccountNumber != request.BankAccountNumber ||
            pro.BankIfsc != request.BankIfsc ||
            pro.UpiVpa != request.UpiVpa;

        pro.PayoutMethod = request.PayoutMethod;
        pro.BankAccountHolderName = request.BankAccountHolderName;
        pro.BankAccountNumber = request.BankAccountNumber;
        pro.BankIfsc = request.BankIfsc;
        pro.UpiVpa = request.UpiVpa;
        pro.UpdatedAt = DateTime.UtcNow;

        if (detailsChanged)
            pro.RazorpayFundAccountId = null;

        await _context.SaveChangesAsync();

        // Retry any pending payouts now that bank details are set
        var pendingPayouts = await _context.Payouts
            .Where(p => p.ProId == id && (p.Status == "Pending" || p.Status == "Failed"))
            .ToListAsync();

        foreach (var payout in pendingPayouts)
        {
            try { await _payoutService.ProcessPendingPayoutAsync(payout.Id); }
            catch (Exception ex)
            {
                // non-fatal — logged inside the service
                _ = ex;
            }
        }

        return Ok(new { message = "Bank details updated successfully" });
    }

    private bool ProExists(int id)
    {
        return _context.Pros.Any(e => e.Id == id);
    }
}
