using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Pro")]
public class BusinessesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public BusinessesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private int GetProId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<ProBusinessMembership?> GetActiveMembership(int proId, int businessId) =>
        await _context.ProBusinessMemberships
            .FirstOrDefaultAsync(m => m.ProId == proId && m.BusinessId == businessId && m.Status == "Active");

    // ── POST /api/businesses/pre-register ─────────────────────────────────
    // Unauthenticated: creates a Business with Status=Pending during Pro registration.
    // The BusinessId is passed to Pro register/complete to claim ownership.

    [HttpPost("pre-register")]
    [AllowAnonymous]
    public async Task<IActionResult> PreRegister([FromBody] PreRegisterBusinessRequest req)
    {
        var address = new Address
        {
            AddressType = "Business",
            HouseNameNumber = req.HouseNameNumber ?? string.Empty,
            Street1 = req.Street1 ?? string.Empty,
            Street2 = req.Street2,
            City = req.City ?? string.Empty,
            District = req.District,
            State = req.State ?? string.Empty,
            Country = req.Country ?? string.Empty,
            ZipPostalCode = req.ZipPostalCode ?? string.Empty,
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.Addresses.Add(address);
        await _context.SaveChangesAsync();

        var biz = new Business
        {
            BusinessName = req.BusinessName,
            Phone = req.Phone,
            AddressId = address.Id,
            Status = "Pending",
        };
        _context.Businesses.Add(biz);
        await _context.SaveChangesAsync();

        return Ok(new { businessId = biz.Id });
    }

    // ── GET /api/businesses/mine ───────────────────────────────────────────
    // Returns all businesses the authenticated Pro belongs to

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var proId = GetProId();
        var memberships = await _context.ProBusinessMemberships
            .Include(m => m.Business).ThenInclude(b => b.Address)
            .Include(m => m.Business).ThenInclude(b => b.Members)
            .Where(m => m.ProId == proId && m.Status == "Active")
            .ToListAsync();

        var result = memberships.Select(m => new
        {
            m.Business.Id,
            m.Business.BusinessName,
            m.Business.Description,
            m.Business.Status,
            m.Business.ServiceRadiusKm,
            m.Role,
            m.JoinedAt,
            Address = m.Business.Address == null ? null : new
            {
                m.Business.Address.Street1,
                m.Business.Address.City,
                m.Business.Address.State,
                m.Business.Address.Country,
                m.Business.Address.ZipPostalCode,
            },
            MemberCount = m.Business.Members?.Count(mem => mem.Status == "Active") ?? 0,
        });

        return Ok(result);
    }

    // ── GET /api/businesses/{id} ───────────────────────────────────────────

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var proId = GetProId();
        var membership = await GetActiveMembership(proId, id);
        if (membership == null) return Forbid();

        var biz = await _context.Businesses
            .Include(b => b.Address)
            .Include(b => b.Members).ThenInclude(m => m.Pro)
            .Include(b => b.Services)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (biz == null) return NotFound();

        return Ok(new
        {
            biz.Id,
            biz.BusinessName,
            biz.Description,
            biz.Phone,
            biz.Status,
            biz.CreatedAt,
            Address = biz.Address == null ? null : new
            {
                biz.Address.HouseNameNumber,
                biz.Address.Street1,
                biz.Address.Street2,
                biz.Address.City,
                biz.Address.District,
                biz.Address.State,
                biz.Address.Country,
                biz.Address.ZipPostalCode,
                biz.Address.Latitude,
                biz.Address.Longitude,
            },
            Members = biz.Members?
                .Where(m => m.Status == "Active")
                .Select(m => new
                {
                    m.Id,
                    m.ProId,
                    ProName = m.Pro?.ProName,
                    m.Role,
                    m.JoinedAt,
                }),
            ServiceCount = biz.Services?.Count ?? 0,
        });
    }

    // ── POST /api/businesses ───────────────────────────────────────────────
    // Create a new business. The caller becomes Owner.

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBusinessRequest req)
    {
        var proId = GetProId();

        var address = new Address
        {
            HouseNameNumber = req.HouseNameNumber,
            Street1 = req.Street1,
            Street2 = req.Street2,
            City = req.City,
            District = req.District,
            State = req.State,
            Country = req.Country ?? string.Empty,
            ZipPostalCode = req.ZipPostalCode,
            Latitude = req.Latitude,
            Longitude = req.Longitude,
        };
        _context.Addresses.Add(address);
        await _context.SaveChangesAsync();

        var business = new Business
        {
            BusinessName = req.BusinessName,
            Description = req.Description,
            Phone = req.Phone,
            AddressId = address.Id,
        };
        _context.Businesses.Add(business);
        await _context.SaveChangesAsync();

        var membership = new ProBusinessMembership
        {
            ProId = proId,
            BusinessId = business.Id,
            Role = "Owner",
            Status = "Active",
        };
        _context.ProBusinessMemberships.Add(membership);

        if (req.MigrateSoloServices)
        {
            await _context.Services
                .Where(s => s.ProId == proId && s.BusinessId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.BusinessId, business.Id));
        }

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = business.Id }, new
        {
            business.Id,
            business.BusinessName,
        });
    }

    // ── PUT /api/businesses/{id} ───────────────────────────────────────────
    // Update business name/description. Owner only.

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBusinessRequest req)
    {
        var proId = GetProId();
        var membership = await GetActiveMembership(proId, id);
        if (membership == null || membership.Role != "Owner") return Forbid();

        var biz = await _context.Businesses.FindAsync(id);
        if (biz == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(req.BusinessName)) biz.BusinessName = req.BusinessName;
        if (req.Description != null) biz.Description = req.Description;
        if (req.ServiceRadiusKm.HasValue) biz.ServiceRadiusKm = req.ServiceRadiusKm;
        biz.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { biz.Id, biz.BusinessName, biz.Description, biz.ServiceRadiusKm });
    }

    // ── GET /api/businesses/{id}/members ──────────────────────────────────

    [HttpGet("{id}/members")]
    public async Task<IActionResult> GetMembers(int id)
    {
        var proId = GetProId();
        var membership = await GetActiveMembership(proId, id);
        if (membership == null) return Forbid();

        var members = await _context.ProBusinessMemberships
            .Include(m => m.Pro)
            .Where(m => m.BusinessId == id && m.Status == "Active")
            .ToListAsync();

        return Ok(members.Select(m => new
        {
            m.Id,
            m.ProId,
            ProName = m.Pro?.ProName,
            ProEmail = m.Pro?.Email,
            m.Role,
            m.JoinedAt,
        }));
    }

    // ── POST /api/businesses/{id}/members ─────────────────────────────────
    // Add an existing Pro to the business by proId. Owner only.

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(int id, [FromBody] AddMemberRequest req)
    {
        var ownerId = GetProId();
        var ownerMembership = await GetActiveMembership(ownerId, id);
        if (ownerMembership == null || ownerMembership.Role != "Owner") return Forbid();

        var pro = await _context.Pros.FirstOrDefaultAsync(p => p.Email == req.Email);
        if (pro == null) return BadRequest(new { message = "No Pro found with that email." });

        var existing = await _context.ProBusinessMemberships
            .FirstOrDefaultAsync(m => m.ProId == pro.Id && m.BusinessId == id);

        if (existing != null)
        {
            if (existing.Status == "Active")
                return Conflict(new { message = "Pro is already a member." });
            existing.Status = "Active";
            existing.JoinedAt = DateTime.UtcNow;
        }
        else
        {
            _context.ProBusinessMemberships.Add(new ProBusinessMembership
            {
                ProId = pro.Id,
                BusinessId = id,
                Role = req.Role ?? "Member",
                Status = "Active",
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Member added." });
    }

    // ── DELETE /api/businesses/{id}/members/{membershipId} ────────────────
    // Remove a member. Owner can remove any Member; a Pro can remove themselves.

    [HttpDelete("{id}/members/{membershipId}")]
    public async Task<IActionResult> RemoveMember(int id, int membershipId)
    {
        var proId = GetProId();
        var ownerMembership = await GetActiveMembership(proId, id);

        var target = await _context.ProBusinessMemberships
            .FirstOrDefaultAsync(m => m.Id == membershipId && m.BusinessId == id);
        if (target == null) return NotFound();

        var isSelf = target.ProId == proId;
        var isOwner = ownerMembership?.Role == "Owner";

        if (!isSelf && !isOwner) return Forbid();

        target.Status = "Revoked";
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── POST /api/businesses/{id}/migrate-services ────────────────────────
    // Migrate all solo Pro services to this Business.

    [HttpPost("{id}/migrate-services")]
    public async Task<IActionResult> MigrateServices(int id)
    {
        var proId = GetProId();
        var membership = await GetActiveMembership(proId, id);
        if (membership == null) return Forbid();

        var count = await _context.Services
            .Where(s => s.ProId == proId && s.BusinessId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.BusinessId, id));

        return Ok(new { migrated = count });
    }
}

// ── Request models ─────────────────────────────────────────────────────────

public class PreRegisterBusinessRequest
{
    [Required]
    [StringLength(200)]
    public string BusinessName { get; set; } = null!;

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(100)]
    public string? HouseNameNumber { get; set; }
    [StringLength(200)]
    public string? Street1 { get; set; }
    [StringLength(200)]
    public string? Street2 { get; set; }
    [StringLength(100)]
    public string? City { get; set; }
    [StringLength(100)]
    public string? District { get; set; }
    [StringLength(100)]
    public string? State { get; set; }
    [StringLength(100)]
    public string? Country { get; set; }
    [StringLength(20)]
    public string? ZipPostalCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class CreateBusinessRequest
{
    [Required]
    [StringLength(200)]
    public string BusinessName { get; set; } = null!;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    // Address fields
    [StringLength(100)]
    public string? HouseNameNumber { get; set; }
    [StringLength(200)]
    public string? Street1 { get; set; }
    [StringLength(200)]
    public string? Street2 { get; set; }
    [StringLength(100)]
    public string? City { get; set; }
    [StringLength(100)]
    public string? District { get; set; }
    [StringLength(100)]
    public string? State { get; set; }
    [StringLength(100)]
    public string? Country { get; set; }
    [StringLength(20)]
    public string? ZipPostalCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public bool MigrateSoloServices { get; set; } = false;
}

public class UpdateBusinessRequest
{
    [StringLength(200)]
    public string? BusinessName { get; set; }
    [StringLength(1000)]
    public string? Description { get; set; }
    [Range(1, 500)]
    public int? ServiceRadiusKm { get; set; }
}

public class AddMemberRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;
    public string? Role { get; set; } // 'Owner' | 'Member'
}
