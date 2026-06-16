using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using MyAuthSystem.Application.Authentication.Commands;

namespace MyAuthSystem.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")] // URL เป็น api/auth
public class AuthController : ControllerBase
{
    private readonly IValidator<LoginCommand> _loginValidator;
    private readonly LoginCommandHandler _loginHandler;

    private readonly IValidator<RefreshTokenCommand> _refreshValidator;
    private readonly RefreshTokenCommandHandler _refreshHandler;


    public AuthController(
        IValidator<LoginCommand> loginValidator,
        LoginCommandHandler loginHandler,
        IValidator<RefreshTokenCommand> refreshValidator,
        RefreshTokenCommandHandler refreshHandler)
    {
        _loginValidator = loginValidator;
        _loginHandler = loginHandler;
        _refreshValidator = refreshValidator;
        _refreshHandler = refreshHandler;
    }

    // URL เป็น api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var validationResult = await _loginValidator.ValidateAsync(command);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.ToDictionary());
        }

        try
        {
            var authResult = await _loginHandler.HandleAsync(command);

            // ส่ง Refresh Token กลับไปแบบ HttpOnly Cookie
            SetRefreshTokenCookie(authResult.RefreshToken);

            return Ok(new { AccessToken = authResult.AccessToken });

        }
        catch (Exception)
        {
            return Unauthorized();
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto request)
    {
        // อ่านค่า Refresh Token จาก HttpOnly Cookie ที่ Client ส่งมา
        var refreshToken = Request.Cookies["refreshToken"];

        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { Message = "Refresh token is missing" });
        }

        var command = new RefreshTokenCommand(request.ExpiredAccessToken, refreshToken);
        var validationResult = await _refreshValidator.ValidateAsync(command);
        if (!validationResult.IsValid) return BadRequest(validationResult.ToDictionary());

        try
        {
            // ส่งไปที่ Handler เพื่อยกเลิกตั๋วเก่าและออกตั๋วชุดใหม่
            var authResult = await _refreshHandler.HandleAsync(command);

            // แนบคุกกี้ Refresh Token ใบใหม่กลับไป (Rotation)
            SetRefreshTokenCookie(authResult.RefreshToken);

            return Ok(new { AccessToken = authResult.AccessToken });
        }
        catch (Exception)
        {
            return Unauthorized();
        }
    }

    // 🛠️ Helper Method สำหรับตั้งค่า Cookie ให้ปลอดภัย
    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,   // ป้องกันสคริปต์แฝง (XSS) แอบอ่านค่า
            Secure = true,     // ส่งผ่าน HTTPS เท่านั้น
            SameSite = SameSiteMode.Strict, // ป้องกัน CSRF
            Expires = DateTime.UtcNow.AddDays(7)
        };

        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }
}

// DTO แบบง่ายสำหรับรับเฉพาะ Access Token ที่หมดอายุจาก Body
public record RefreshRequestDto(string ExpiredAccessToken);