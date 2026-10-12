using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Services;

namespace ServiceProviderAPI.Tests;

public class TokenBlacklistServiceTests
{
    [Fact]
    public async Task RevokeAsync_ThenIsRevoked_ReturnsTrue()
    {
        var db = TestSupport.NewDb();
        var svc = new TokenBlacklistService(db);

        await svc.RevokeAsync("jti-active", DateTime.UtcNow.AddMinutes(15));

        Assert.True(await svc.IsRevokedAsync("jti-active"));
    }

    [Fact]
    public async Task RevokeAsync_IsIdempotent()
    {
        var db = TestSupport.NewDb();
        var svc = new TokenBlacklistService(db);
        var expiry = DateTime.UtcNow.AddMinutes(15);

        await svc.RevokeAsync("jti-dupe", expiry);
        await svc.RevokeAsync("jti-dupe", expiry);

        // The second revoke must not insert a duplicate row.
        Assert.Equal(1, await db.RevokedTokens.CountAsync(rt => rt.Jti == "jti-dupe"));
    }

    [Fact]
    public async Task IsRevokedAsync_ExpiredToken_ReturnsFalse()
    {
        var db = TestSupport.NewDb();
        var svc = new TokenBlacklistService(db);

        // Already-expired revocation: still in the table, but no longer "revoked" for gating.
        await svc.RevokeAsync("jti-expired", DateTime.UtcNow.AddMinutes(-1));

        Assert.False(await svc.IsRevokedAsync("jti-expired"));
    }

    [Fact]
    public async Task IsRevokedAsync_UnknownJti_ReturnsFalse()
    {
        var db = TestSupport.NewDb();
        var svc = new TokenBlacklistService(db);

        Assert.False(await svc.IsRevokedAsync("never-seen"));
    }

    [Fact]
    public async Task PurgeExpiredAsync_RemovesOnlyExpired()
    {
        var db = TestSupport.NewDb();
        var svc = new TokenBlacklistService(db);
        await svc.RevokeAsync("jti-old", DateTime.UtcNow.AddMinutes(-5));
        await svc.RevokeAsync("jti-new", DateTime.UtcNow.AddMinutes(30));

        await svc.PurgeExpiredAsync();

        Assert.False(await db.RevokedTokens.AnyAsync(rt => rt.Jti == "jti-old"));
        Assert.True(await db.RevokedTokens.AnyAsync(rt => rt.Jti == "jti-new"));
    }
}
