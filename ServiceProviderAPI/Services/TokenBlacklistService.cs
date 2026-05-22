using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;

namespace ServiceProviderAPI.Services;

public interface ITokenBlacklistService
{
    Task RevokeAsync(string jti, DateTime expiresAt);
    Task<bool> IsRevokedAsync(string jti);
    Task PurgeExpiredAsync();
}

public class TokenBlacklistService : ITokenBlacklistService
{
    private readonly ApplicationDbContext _context;

    public TokenBlacklistService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task RevokeAsync(string jti, DateTime expiresAt)
    {
        var already = await _context.RevokedTokens.AnyAsync(rt => rt.Jti == jti);
        if (!already)
        {
            _context.RevokedTokens.Add(new RevokedToken
            {
                Jti = jti,
                ExpiresAt = expiresAt,
                RevokedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> IsRevokedAsync(string jti)
    {
        return await _context.RevokedTokens
            .AnyAsync(rt => rt.Jti == jti && rt.ExpiresAt > DateTime.UtcNow);
    }

    public async Task PurgeExpiredAsync()
    {
        var expired = await _context.RevokedTokens
            .Where(rt => rt.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync();
        if (expired.Count > 0)
        {
            _context.RevokedTokens.RemoveRange(expired);
            await _context.SaveChangesAsync();
        }
    }
}
