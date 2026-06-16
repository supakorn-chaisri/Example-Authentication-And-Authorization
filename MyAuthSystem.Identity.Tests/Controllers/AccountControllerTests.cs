using Xunit;
using NSubstitute;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using MyAuthSystem.Identity.Controllers;
using MyAuthSystem.Identity.Models;
using MyAuthSystem.Identity.Services;

namespace MyAuthSystem.Identity.Tests.Controllers;

public class AccountControllerTests
{
    // ประกาศตัวแปรเพื่อถือสารสารจำลอง (Mock)
    private readonly UserManager<ApplicationUser> _mockUserManager;
    private readonly SignInManager<ApplicationUser> _mockSignInManager;
    private readonly ITokenService _mockTokenService;
    private readonly AccountController _controller;

    public AccountControllerTests()
    {
        // 1. นำเทคนิค Mocking มาจำลอง Class ที่ซับซ้อนของ Microsoft ด้วย NSubstitute
        _mockUserManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);

        _mockSignInManager = Substitute.For<SignInManager<ApplicationUser>>(
            _mockUserManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null, null, null, null);

        _mockTokenService = Substitute.For<ITokenService>();

        // 2. ประกอบร่าง Controller ตัวจริงที่จะใช้เทส โดยยัดเยียด Mock Services เข้าไปแทน
        _controller = new AccountController(_mockUserManager, _mockSignInManager, _mockTokenService);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200OK_WithTokens()
    {
        // Arrange (เตรียมข้อมูล และเซ็ตพฤติกรรมจำลอง)
        var loginDto = new LoginDto { Email = "test@example.com", Password = "ValidPassword123!" };
        var fakeUser = new ApplicationUser { Id = "user-abc", Email = loginDto.Email, FirstName = "Somchai" };

        // กำหนดล่วงหน้าว่า ถ้าค้นหาด้วยอีเมลนี้ ให้คืนค่า fakeUser กลับไป
        _mockUserManager.FindByEmailAsync(loginDto.Email).Returns(fakeUser);

        // กำหนดว่า ถ้าตรวจสอบรหัสผ่าน ให้คืนค่าเป็น ผลลัพธ์สำเร็จ (Succeeded)
        _mockSignInManager.CheckPasswordSignInAsync(fakeUser, loginDto.Password, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        // กำหนดว่า ถ้าออกตั๋วคู่ ให้พ่นโทเค็นจำลองกลับมา
        _mockTokenService.GenerateTokensAsync(fakeUser).Returns(("mock-access-token", "mock-refresh-token"));

        // Act (สั่งให้ฟังก์ชันทำงานจริง)
        var result = await _controller.Login(loginDto);

        // Assert (ตรวจสอบผลลัพธ์ด้วย FluentAssertions)
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        // ตรวจเช็กว่าโครงสร้าง JSON มีตั๋วพ่นกลับไปหาหน้าบ้านจริงไหม
        var responseBody = okResult.Value;
        responseBody.Should().BeEquivalentTo(new
        {
            message = "เข้าสู่ระบบสำเร็จ",
            accessToken = "mock-access-token",
            refreshToken = "mock-refresh-token"
        });
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401Unauthorized()
    {
        // Arrange (เซ็ตพฤติกรรมให้ล็อกอินล้มเหลว)
        var loginDto = new LoginDto { Email = "test@example.com", Password = "WrongPassword!" };
        var fakeUser = new ApplicationUser { Id = "user-abc", Email = loginDto.Email };

        _mockUserManager.FindByEmailAsync(loginDto.Email).Returns(fakeUser);

        // 💡 กำหนดให้รันผลลัพธ์ออกมาเป็น ล้มเหลว (Failed)
        _mockSignInManager.CheckPasswordSignInAsync(fakeUser, loginDto.Password, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        // Act
        var result = await _controller.Login(loginDto);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }
}