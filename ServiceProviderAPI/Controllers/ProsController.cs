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
    private readonly IGeocodingService _geocoding;

    public ProsController(ApplicationDbContext context, IPayoutService payoutService, IGeocodingService geocoding)
    {
        _context = context;
        _payoutService = payoutService;
        _geocoding = geocoding;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<object>>> GetPros()
    {
        var pros = await _context.Pros.Include(p => p.Services).Include(p => p.Address).ToListAsync();
        return Ok(pros.Select(p => SafePro(p)));
    }

    [HttpGet("browse")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> BrowsePros(
        [FromQuery] string? search = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] string? country = null,
        [FromQuery] string? state = null,
        [FromQuery] string? district = null,
        [FromQuery] string? pinCode = null,
        [FromQuery] double? lat = null,
        [FromQuery] double? lng = null,
        [FromQuery] double? radiusKm = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        // Proximity mode: when the searcher's coordinates are supplied, rank by distance
        // to the pro rather than restricting to an exact PIN.
        bool proximity = lat.HasValue && lng.HasValue;

        var query = _context.Pros
            .Include(p => p.Services)
            .Include(p => p.Address)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(p =>
                p.ProName.ToLower().Contains(term) ||
                p.BusinessName.ToLower().Contains(term) ||
                (p.Address != null && p.Address.City != null && p.Address.City.ToLower().Contains(term)) ||
                p.Services.Any(s => s.Name.ToLower().Contains(term)));
        }

        if (categoryId.HasValue)
            query = query.Where(p => p.Services.Any(s => s.ServiceCategoryId == categoryId.Value));

        if (!string.IsNullOrWhiteSpace(country))
            query = query.Where(p => p.Address != null && p.Address.Country != null && p.Address.Country.ToLower() == country.ToLower());

        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(p => p.Address != null && p.Address.State != null && p.Address.State.ToLower() == state.ToLower());

        if (!string.IsNullOrWhiteSpace(district))
            query = query.Where(p => p.Address != null && p.Address.District != null && p.Address.District.ToLower() == district.ToLower());

        // Skipped in proximity mode — the client geocodes the PIN into lat/lng so we rank by
        // distance instead (pros in adjacent PINs should still surface).
        if (!proximity && !string.IsNullOrWhiteSpace(pinCode))
            query = query.Where(p => p.Address != null && p.Address.ZipPostalCode != null && p.Address.ZipPostalCode == pinCode);

        pageSize = Math.Clamp(pageSize, 1, 50);
        page = Math.Max(1, page);

        if (proximity)
        {
            // Pull the filtered set (no Skip/Take) so Haversine can run in memory, then
            // rank nearest-first and paginate.
            var all = await query.ToListAsync();
            var withDistance = all.Select(p =>
            {
                double? dist = null;
                if (p.Address?.Latitude != null && p.Address?.Longitude != null)
                    dist = Math.Round(HaversineKm(lat!.Value, lng!.Value, p.Address.Latitude!.Value, p.Address.Longitude!.Value), 1);
                return (Pro: p, DistanceKm: dist);
            });

            // Optional hard radius filter: keep pros within radius OR those with no
            // coordinates (can't position them, so don't silently drop them).
            if (radiusKm.HasValue)
                withDistance = withDistance.Where(x => x.DistanceKm == null || x.DistanceKm <= radiusKm.Value);

            var ordered = withDistance
                .OrderBy(x => x.DistanceKm ?? double.MaxValue)
                .ThenBy(x => x.Pro.ProName)
                .ToList();

            var itemsP = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Pro.Id, x.Pro.ProName, x.Pro.BusinessName,
                    City = x.Pro.Address != null ? x.Pro.Address.City : null,
                    District = x.Pro.Address != null ? x.Pro.Address.District : null,
                    State = x.Pro.Address != null ? x.Pro.Address.State : null,
                    Country = x.Pro.Address != null ? x.Pro.Address.Country : null,
                    Latitude = x.Pro.Address != null ? x.Pro.Address.Latitude : (double?)null,
                    Longitude = x.Pro.Address != null ? x.Pro.Address.Longitude : (double?)null,
                    x.Pro.ServiceRadiusKm,
                    x.Pro.IsEmailVerified,
                    Services = x.Pro.Services?.Select(s => new { s.Id, s.Name, s.Price }),
                    DistanceKm = x.DistanceKm
                })
                .ToList();

            return Ok(new { total = ordered.Count, page, pageSize, items = itemsP });
        }

        var total = await query.CountAsync();

        var pros = await query
            .OrderBy(p => p.ProName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new
        {
            total,
            page,
            pageSize,
            items = pros.Select(p => new
            {
                p.Id, p.ProName, p.BusinessName,
                City = p.Address != null ? p.Address.City : null,
                District = p.Address != null ? p.Address.District : null,
                State = p.Address != null ? p.Address.State : null,
                Country = p.Address != null ? p.Address.Country : null,
                Latitude = p.Address != null ? p.Address.Latitude : (double?)null,
                Longitude = p.Address != null ? p.Address.Longitude : (double?)null,
                p.ServiceRadiusKm,
                p.IsEmailVerified,
                Services = p.Services?.Select(s => new { s.Id, s.Name, s.Price }),
                DistanceKm = (double?)null
            })
        });
    }

    // Haversine formula — returns distance in kilometres between two lat/lng points.
    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<object>> GetPro(int id)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int.TryParse(callerIdStr, out int callerId);
        bool isAdmin = User.IsInRole("Admin");
        bool isPro = User.IsInRole("Pro");

        if (isPro && !isAdmin && callerId != id)
            return Forbid();

        var pro = await _context.Pros
            .Include(p => p.Services)
            .Include(p => p.Address)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (pro == null) return NotFound();

        return Ok(SafePro(pro));
    }

    private static object SafePro(Pro p) => new
    {
        p.Id, p.ProName, p.BusinessName, p.Email, p.PhoneNumber,
        HouseNameNumber = p.Address != null ? p.Address.HouseNameNumber : null,
        Street1 = p.Address != null ? p.Address.Street1 : null,
        Street2 = p.Address != null ? p.Address.Street2 : null,
        City = p.Address != null ? p.Address.City : null,
        District = p.Address != null ? p.Address.District : null,
        State = p.Address != null ? p.Address.State : null,
        Country = p.Address != null ? p.Address.Country : null,
        ZipPostalCode = p.Address != null ? p.Address.ZipPostalCode : null,
        Latitude = p.Address != null ? p.Address.Latitude : (double?)null,
        Longitude = p.Address != null ? p.Address.Longitude : (double?)null,
        p.ServiceRadiusKm,
        p.CreatedAt, p.UpdatedAt,
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
    public async Task<ActionResult<object>> UpdatePro(int id, [FromBody] UpdateProRequest request)
    {
        if (id != request.Id)
            return BadRequest();

        // A pro may only edit their own profile (admins reach pro management elsewhere).
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        if (!User.IsInRole("Admin") && callerId != id)
            return Forbid();

        var existingPro = await _context.Pros.Include(p => p.Address).FirstOrDefaultAsync(p => p.Id == id);
        if (existingPro == null)
            return NotFound();

        if (request.ProName != null) existingPro.ProName = request.ProName;

        if (request.Email != null && !string.Equals(existingPro.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            existingPro.Email = request.Email;
            existingPro.IsEmailVerified = false;
        }

        if (request.PhoneNumber != null && existingPro.PhoneNumber != request.PhoneNumber)
        {
            existingPro.PhoneNumber = request.PhoneNumber;
            existingPro.IsPhoneVerified = false;
        }

        if (!string.IsNullOrEmpty(request.PasswordHash))
            existingPro.PasswordHash = BC.HashPassword(request.PasswordHash);

        if (request.BusinessName != null) existingPro.BusinessName = request.BusinessName;
        existingPro.ServiceRadiusKm = request.ServiceRadiusKm;
        existingPro.UpdatedAt = DateTime.UtcNow;

        // Update or create address record
        if (existingPro.Address == null)
        {
            var newAddr = new Address { AddressType = "Pro", CreatedAt = DateTime.UtcNow };
            _context.Addresses.Add(newAddr);
            existingPro.Address = newAddr;
        }
        var addr = existingPro.Address;

        // Did the location-defining fields actually change? (compare before overwriting)
        var locationChanged =
            !SameAddressField(addr.HouseNameNumber, request.HouseNameNumber) ||
            !SameAddressField(addr.Street1,         request.Street1) ||
            !SameAddressField(addr.City,            request.City) ||
            !SameAddressField(addr.State,           request.State) ||
            !SameAddressField(addr.Country,         request.Country) ||
            !SameAddressField(addr.ZipPostalCode,   request.ZipPostalCode);

        addr.HouseNameNumber = request.HouseNameNumber;
        addr.Street1 = request.Street1;
        addr.Street2 = request.Street2;
        addr.City = request.City;
        addr.District = request.District;
        addr.State = request.State;
        addr.Country = request.Country;
        addr.ZipPostalCode = request.ZipPostalCode;

        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            // Fresh coordinates supplied (e.g. from address autocomplete) — authoritative.
            addr.Latitude = request.Latitude;
            addr.Longitude = request.Longitude;
        }
        else if (locationChanged)
        {
            // Address text changed but no new coordinates (manual edit) — geocode the new
            // address so coordinates stay accurate. Best-effort: on failure they fall to null
            // (cleared, not stale) and the admin geocode-backfill can still re-resolve later.
            var coords = await _geocoding.TryGeocodeAsync(
                addr.HouseNameNumber, addr.Street1, addr.City, addr.State, addr.Country, HttpContext.RequestAborted);
            addr.Latitude = coords?.Lat;
            addr.Longitude = coords?.Lon;
        }
        addr.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!ProExists(id))
                return NotFound();
            throw;
        }

        return Ok(SafePro(existingPro));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Pro")]
    public async Task<IActionResult> DeletePro(int id)
    {
        // A pro may only delete their own account.
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        if (!User.IsInRole("Admin") && callerId != id)
            return Forbid();

        var pro = await _context.Pros.Include(p => p.Address).FirstOrDefaultAsync(p => p.Id == id);
        if (pro == null)
            return NotFound();

        if (pro.Address != null)
            _context.Addresses.Remove(pro.Address);

        _context.Pros.Remove(pro);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // GET: api/pros/{id}/bank-details — returns the pro's default payout PaymentMethod
    [HttpGet("{id}/bank-details")]
    [Authorize(Roles = "Pro,Admin")]
    public async Task<IActionResult> GetBankDetails(int id)
    {
        var callerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        bool isAdmin = User.IsInRole("Admin");

        if (!isAdmin && callerId != id)
            return Forbid();

        if (!await _context.Pros.AnyAsync(p => p.Id == id))
            return NotFound();

        var pm = await _context.PaymentMethods
            .Where(p => p.ProId == id && p.IsDefault)
            .FirstOrDefaultAsync()
            ?? await _context.PaymentMethods
            .Where(p => p.ProId == id)
            .OrderBy(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        bool hasBankDetails = pm != null && (
            pm.Type == "UPI"
                ? !string.IsNullOrWhiteSpace(pm.UpiVpa)
                : !string.IsNullOrWhiteSpace(pm.BankAccountNumber) && !string.IsNullOrWhiteSpace(pm.BankIfsc));

        return Ok(new ProBankDetailsDto
        {
            PayoutMethod = pm?.Type,
            BankAccountHolderName = pm?.BankAccountHolderName,
            BankAccountNumber = pm?.BankAccountNumber,
            BankIfsc = pm?.BankIfsc,
            UpiVpa = pm?.UpiVpa,
            HasBankDetails = hasBankDetails
        });
    }

    // PUT: api/pros/{id}/bank-details — upserts the pro's default payout PaymentMethod
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

        var pm = await _context.PaymentMethods
            .Where(p => p.ProId == id && p.IsDefault)
            .FirstOrDefaultAsync()
            ?? await _context.PaymentMethods
            .Where(p => p.ProId == id)
            .OrderBy(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        bool detailsChanged = pm == null
            || pm.Type != request.PayoutMethod
            || pm.BankAccountNumber != request.BankAccountNumber
            || pm.BankIfsc != request.BankIfsc
            || pm.UpiVpa != request.UpiVpa;

        if (pm == null)
        {
            pm = new PaymentMethod { ProId = id, IsDefault = true, CreatedAt = DateTime.UtcNow };
            _context.PaymentMethods.Add(pm);
        }

        pm.Type = request.PayoutMethod;
        pm.Label = request.PayoutMethod == "UPI" ? "UPI" : "Bank Account";
        pm.BankAccountHolderName = request.BankAccountHolderName;
        pm.BankAccountNumber = request.BankAccountNumber;
        pm.BankIfsc = request.BankIfsc;
        pm.UpiVpa = request.UpiVpa;

        if (detailsChanged)
            pm.RazorpayFundAccountId = null;

        pro.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var pendingPayouts = await _context.Payouts
            .Where(p => p.ProId == id && (p.Status == "Pending" || p.Status == "Failed"))
            .ToListAsync();

        foreach (var payout in pendingPayouts)
        {
            try { await _payoutService.ProcessPendingPayoutAsync(payout.Id); }
            catch (Exception ex) { _ = ex; }
        }

        return Ok(new { message = "Bank details updated successfully" });
    }

    private bool ProExists(int id)
    {
        return _context.Pros.Any(e => e.Id == id);
    }

    // Treats null/empty/whitespace as equal and ignores case, so trivial edits don't drop coordinates.
    private static bool SameAddressField(string? a, string? b) =>
        string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
}
