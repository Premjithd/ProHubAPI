using Microsoft.EntityFrameworkCore;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Models;
using ServiceProviderAPI.Services;

namespace ServiceProviderAPI.Tests;

/// <summary>
/// Tests for <see cref="VerificationService"/>. The phone path only logs (safe to call);
/// the email *generation* path hits SMTP, so it is intentionally not exercised — email
/// verification is tested by seeding a code row directly and calling VerifyEmailCode.
/// </summary>
public class VerificationServiceTests
{
    private static VerificationService NewService(ApplicationDbContext db) =>
        new(db, TestSupport.Config(new()));

    [Fact]
    public async Task GeneratePhoneCode_PersistsSixDigitUnusedCode()
    {
        var db = TestSupport.NewDb();
        var svc = NewService(db);

        var code = await svc.GenerateAndSendPhoneVerificationCode("9876543210", "User");

        Assert.Matches(@"^\d{6}$", code);
        var row = await db.VerificationCodes.SingleAsync();
        Assert.Equal("9876543210", row.PhoneNumber);
        Assert.Equal("Phone", row.Type);
        Assert.Equal("User", row.UserType);
        Assert.False(row.IsUsed);
        Assert.True(row.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task VerifyPhoneCode_CorrectCode_MarksUsedAndVerifiesUser()
    {
        var db = TestSupport.NewDb();
        db.Users.Add(new User { Id = 1, FirstName = "Phone", LastName = "User", Email = "p@test.com", PhoneNumber = "9000000000", UserType = "User", IsPhoneVerified = false });
        await db.SaveChangesAsync();
        var svc = NewService(db);
        var code = await svc.GenerateAndSendPhoneVerificationCode("9000000000", "User");

        var result = await svc.VerifyPhoneCode("9000000000", code, "User");

        Assert.True(result);
        Assert.True((await db.VerificationCodes.SingleAsync()).IsUsed);
        Assert.True((await db.Users.SingleAsync()).IsPhoneVerified);
    }

    [Fact]
    public async Task VerifyPhoneCode_WrongCode_ReturnsFalse()
    {
        var db = TestSupport.NewDb();
        var svc = NewService(db);
        await svc.GenerateAndSendPhoneVerificationCode("9000000001", "User");

        Assert.False(await svc.VerifyPhoneCode("9000000001", "000000", "User"));
    }

    [Fact]
    public async Task VerifyPhoneCode_ExpiredCode_ReturnsFalse()
    {
        var db = TestSupport.NewDb();
        db.VerificationCodes.Add(new VerificationCode
        {
            Code = "123456",
            PhoneNumber = "9000000002",
            Type = "Phone",
            UserType = "User",
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();
        var svc = NewService(db);

        Assert.False(await svc.VerifyPhoneCode("9000000002", "123456", "User"));
    }

    [Fact]
    public async Task VerifyPhoneCode_CannotBeReused()
    {
        var db = TestSupport.NewDb();
        var svc = NewService(db);
        var code = await svc.GenerateAndSendPhoneVerificationCode("9000000003", "User");

        Assert.True(await svc.VerifyPhoneCode("9000000003", code, "User"));
        // Second attempt with the now-used code must fail.
        Assert.False(await svc.VerifyPhoneCode("9000000003", code, "User"));
    }

    [Fact]
    public async Task VerifyEmailCode_CorrectCode_VerifiesUser()
    {
        var db = TestSupport.NewDb();
        db.Users.Add(new User { Id = 5, FirstName = "Email", LastName = "User", Email = "e@test.com", UserType = "User", IsEmailVerified = false });
        db.VerificationCodes.Add(new VerificationCode
        {
            Code = "654321",
            Email = "e@test.com",
            Type = "Email",
            UserType = "User",
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
        });
        await db.SaveChangesAsync();
        var svc = NewService(db);

        var result = await svc.VerifyEmailCode("e@test.com", "654321", "User");

        Assert.True(result);
        Assert.True((await db.Users.SingleAsync()).IsEmailVerified);
    }

    [Fact]
    public async Task VerifyEmailCode_WrongUserType_ReturnsFalse()
    {
        var db = TestSupport.NewDb();
        db.VerificationCodes.Add(new VerificationCode
        {
            Code = "111111",
            Email = "mismatch@test.com",
            Type = "Email",
            UserType = "User",
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
        });
        await db.SaveChangesAsync();
        var svc = NewService(db);

        // Same code but requested as a Pro → no match.
        Assert.False(await svc.VerifyEmailCode("mismatch@test.com", "111111", "Pro"));
    }
}
