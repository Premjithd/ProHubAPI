using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ServiceProviderAPI.Models;
using ServiceProviderAPI.Services;

namespace ServiceProviderAPI.Tests;

public class JwtServiceTests
{
    // HS256 requires a key of at least 256 bits (32 bytes).
    private static JwtService NewService() => new(TestSupport.Config(new()
    {
        ["Jwt:Key"] = "this-is-a-sufficiently-long-test-signing-key-0123456789",
        ["Jwt:Issuer"] = "https://test-issuer",
        ["Jwt:Audience"] = "https://test-audience",
    }));

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void GenerateToken_ReturnsTokenAndGuidJti()
    {
        var svc = NewService();
        var user = new User { Id = 7, Email = "u@test.com", FirstName = "Asha" };

        var (token, jti) = svc.GenerateToken(user, "User");

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(Guid.TryParse(jti, out _));
        // The jti claim embedded in the token matches the returned jti.
        Assert.Equal(jti, Read(token).Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
    }

    [Fact]
    public void GenerateToken_EmbedsIdentityClaims()
    {
        var svc = NewService();
        var user = new User { Id = 42, Email = "claims@test.com", FirstName = "Ravi" };

        var (token, _) = svc.GenerateToken(user, "Admin");
        var jwt = Read(token);

        Assert.Equal("42", jwt.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("claims@test.com", jwt.Claims.Single(c => c.Type == ClaimTypes.Email).Value);
        Assert.Equal("Admin", jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal("Ravi", jwt.Claims.Single(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal("https://test-issuer", jwt.Issuer);
        Assert.Contains("https://test-audience", jwt.Audiences);
    }

    [Fact]
    public void GenerateToken_UsesProNameForProEntities()
    {
        var svc = NewService();
        var pro = new Pro
        {
            Id = 3,
            Email = "pro@test.com",
            ProName = "FixIt Services",
            PhoneNumber = "9999999999",
            BusinessName = "FixIt",
        };

        var (token, _) = svc.GenerateToken(pro, "Pro");

        Assert.Equal("FixIt Services", Read(token).Claims.Single(c => c.Type == ClaimTypes.Name).Value);
    }

    [Fact]
    public void GenerateToken_FallsBackToNameProperty()
    {
        var svc = NewService();
        // No FirstName / ProName property → the Name fallback branch is used.
        var principal = new NamedPrincipal { Id = 1, Email = "n@test.com", Name = "Generic User" };

        var (token, _) = svc.GenerateToken(principal, "User");

        Assert.Equal("Generic User", Read(token).Claims.Single(c => c.Type == ClaimTypes.Name).Value);
    }

    [Fact]
    public void GenerateToken_RespectsExpiryMinutes()
    {
        var svc = NewService();
        var user = new User { Id = 1, Email = "exp@test.com", FirstName = "T" };

        var before = DateTime.UtcNow;
        var (token, _) = svc.GenerateToken(user, "User", expiryMinutes: 60);
        var jwt = Read(token);

        // Expiry should be ~60 minutes out (allow a minute of slack for execution time).
        var expectedMin = before.AddMinutes(59);
        var expectedMax = before.AddMinutes(61);
        Assert.InRange(jwt.ValidTo, expectedMin, expectedMax);
    }

    private sealed class NamedPrincipal
    {
        public int Id { get; init; }
        public string? Email { get; init; }
        public string? Name { get; init; }
    }
}
