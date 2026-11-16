using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServiceProviderAPI.Services;

namespace ServiceProviderAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VerificationController : ControllerBase
{
    private readonly IVerificationService _verificationService;

    public VerificationController(IVerificationService verificationService)
    {
        _verificationService = verificationService;
    }

    [HttpPost("send-email-code")]
    [EnableRateLimiting("verification-send")]
    public async Task<IActionResult> SendEmailVerificationCode([FromBody] SendVerificationCodeRequest request)
    {
        try
        {
            var code = await _verificationService.GenerateAndSendEmailVerificationCode(request.Contact, request.UserType);
            return Ok(new { message = "Verification code sent successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Failed to send verification code", error = ex.Message });
        }
    }

    [HttpPost("send-phone-code")]
    [EnableRateLimiting("verification-send")]
    public async Task<IActionResult> SendPhoneVerificationCode([FromBody] SendVerificationCodeRequest request)
    {
        try
        {
            var code = await _verificationService.GenerateAndSendPhoneVerificationCode(request.Contact, request.UserType);
            return Ok(new { message = "Verification code sent successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Failed to send verification code", error = ex.Message });
        }
    }

    [HttpPost("verify-email")]
    [EnableRateLimiting("verification-verify")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyCodeRequest request)
    {
        try
        {
            var isValid = await _verificationService.VerifyEmailCode(request.Contact, request.Code, request.UserType);
            if (isValid)
                return Ok(new { message = "Email verified successfully" });
            return BadRequest(new { message = "Invalid or expired verification code" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Verification failed", error = ex.Message });
        }
    }

    [HttpPost("verify-phone")]
    [EnableRateLimiting("verification-verify")]
    public async Task<IActionResult> VerifyPhone([FromBody] VerifyCodeRequest request)
    {
        try
        {
            var isValid = await _verificationService.VerifyPhoneCode(request.Contact, request.Code, request.UserType);
            if (isValid)
                return Ok(new { message = "Phone number verified successfully" });
            return BadRequest(new { message = "Invalid or expired verification code" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Verification failed", error = ex.Message });
        }
    }
}

public class SendVerificationCodeRequest
{
    public string Contact { get; set; }
    public string UserType { get; set; }  // "User" or "Pro"
}

public class VerifyCodeRequest
{
    public string Contact { get; set; }
    public string Code { get; set; }
    public string UserType { get; set; }  // "User" or "Pro"
}
