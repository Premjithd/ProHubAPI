using Microsoft.AspNetCore.Mvc;
using ServiceProviderAPI.Controllers;
using ServiceProviderAPI.Services;

namespace ServiceProviderAPI.Tests.Controllers;

/// <summary>
/// Tests the HTTP-response mapping of <see cref="VerificationController"/> using a
/// configurable fake service — the controller's job is translating service outcomes
/// (success / false / throw) into Ok / BadRequest responses.
/// </summary>
public class VerificationControllerTests
{
    private sealed class FakeVerificationService : IVerificationService
    {
        public bool VerifyResult { get; init; } = true;
        public Exception? Throw { get; init; }

        public Task<string> GenerateAndSendEmailVerificationCode(string email, string userType) =>
            Throw is not null ? Task.FromException<string>(Throw) : Task.FromResult("123456");

        public Task<string> GenerateAndSendPhoneVerificationCode(string phoneNumber, string userType) =>
            Throw is not null ? Task.FromException<string>(Throw) : Task.FromResult("123456");

        public Task<bool> VerifyEmailCode(string email, string code, string userType) =>
            Throw is not null ? Task.FromException<bool>(Throw) : Task.FromResult(VerifyResult);

        public Task<bool> VerifyPhoneCode(string phoneNumber, string code, string userType) =>
            Throw is not null ? Task.FromException<bool>(Throw) : Task.FromResult(VerifyResult);
    }

    private static SendVerificationCodeRequest SendReq() => new() { Contact = "u@test.com", UserType = "User" };
    private static VerifyCodeRequest VerifyReq() => new() { Contact = "u@test.com", Code = "123456", UserType = "User" };

    [Fact]
    public async Task SendEmailCode_Success_ReturnsOk()
    {
        var controller = new VerificationController(new FakeVerificationService());

        var result = await controller.SendEmailVerificationCode(SendReq());

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task SendEmailCode_ServiceThrows_ReturnsBadRequest()
    {
        var controller = new VerificationController(
            new FakeVerificationService { Throw = new InvalidOperationException("smtp down") });

        var result = await controller.SendEmailVerificationCode(SendReq());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task VerifyEmail_ValidCode_ReturnsOk()
    {
        var controller = new VerificationController(new FakeVerificationService { VerifyResult = true });

        var result = await controller.VerifyEmail(VerifyReq());

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task VerifyEmail_InvalidCode_ReturnsBadRequest()
    {
        var controller = new VerificationController(new FakeVerificationService { VerifyResult = false });

        var result = await controller.VerifyEmail(VerifyReq());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task VerifyPhone_ValidCode_ReturnsOk()
    {
        var controller = new VerificationController(new FakeVerificationService { VerifyResult = true });

        var result = await controller.VerifyPhone(VerifyReq());

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task VerifyPhone_ServiceThrows_ReturnsBadRequest()
    {
        var controller = new VerificationController(
            new FakeVerificationService { Throw = new Exception("boom") });

        var result = await controller.VerifyPhone(VerifyReq());

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
