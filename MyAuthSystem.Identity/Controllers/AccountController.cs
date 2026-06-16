using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MyAuthSystem.Identity.Models;
using MyAuthSystem.Identity.Services;

namespace MyAuthSystem.Identity.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly TokenService _tokenService;

    // Inject บริการจัดการผู้ใช้ของระบบ Identity เข้ามาใช้งาน
    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        TokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // ตรวจสอบก Email นี้เคยลงทะเบียนไปแล้วหรือยัง
        var userExists = await _userManager.FindByEmailAsync(model.Email);
        if (userExists != null)
        {
            return BadRequest(new { message = "อีเมลนี้ถูกใช้งานในระบบแล้ว" });
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName
        };

        // บันทึกผู้ใช้ลง db
        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            return Ok(new { message = "สมัครสมาชิกเสร็จสมบูรณ์" });
        }

        return BadRequest(result.Errors);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return Unauthorized(new { message = "อีเมลหรือรหัสผ่านไม่ถูกต้อง" });
        }

        // ตรวจสอบรหัสผ่าน SignInManager (ล็อกบัญชีอัตโนมัติหากใส่ผิดเกินกำหนด)
        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var tokens = await _tokenService.GenerateTokensAsync(user);

            // ในขั้นตอนนี้ผู้ใช้พิสูจน์ตัวตนผ่านแล้ว
            // ส่งข้อมูลผู้ใช้คนนี้ไปให้ระบบ Token Service เพื่อออก JWT
            return Ok(new
            {
                message = "เข้าสู่ระบบสำเร็จ",
                accessToken = tokens.AccessToken,
                refreshToken = tokens.RefreshToken
            });
        }

        if (result.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status423Locked, new { message = "บัญชีนี้ถูกล็อกชั่วคราวเนื่องจากล็อกอินผิดพลาดหลายครั้ง" });
        }

        return Unauthorized(new { message = "อีเมลหรือรหัสผ่านไม่ถูกต้อง" });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] TokenRequestDto model)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _tokenService.VerifyAndRotateTokenAsync(model);

        if (result == null)
        {
            return Unauthorized(new { message = "Token ไม่ถูกต้อง" });
        }

        return Ok(new
        {
            accessToken = result.Value.AccessToken,
            refreshToken = result.Value.RefreshToken
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] string refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken)) return BadRequest("Token สิ้นสุดสภาพ");

        var isRevoked = await _tokenService.RevokeRefreshTokenAsync(refreshToken);

        if (!isRevoked) return BadRequest(new { message = "ไม่พบ Token หรือข้อมูลไม่ถูกต้อง" });

        return Ok(new { message = "ออกจากระบบเรียบร้อย" });
    }

}