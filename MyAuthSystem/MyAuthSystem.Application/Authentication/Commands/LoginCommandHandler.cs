using MyAuthSystem.Application.Common.Interfaces;
using MyAuthSystem.Domain.Entities;

namespace MyAuthSystem.Application.Authentication.Commands;

public class LoginCommandHandler
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IRefreshTokenRepository _tokenRepository;

    public LoginCommandHandler(IJwtTokenGenerator jwtTokenGenerator, IRefreshTokenRepository tokenRepository)
    {
        _jwtTokenGenerator = jwtTokenGenerator;
        _tokenRepository = tokenRepository;
    }

    public async Task<AuthResult> HandleAsync(LoginCommand command)
    {
        // จำลองการเช็ก User และ Password 
        if (command.Username == "admin" && command.Password == "password123")
        {
            // สร้าง Access Token (JWT)
            var accessToken = _jwtTokenGenerator.GenerateToken(
                userId: "1",
                username: command.Username,
                role: "Admin"
            );

            var refreshTokenString = Guid.NewGuid().ToString();

            // บันทึก Refresh Token ลง db ผ่าน Repository
            var refreshTokenEntity = new UserRefreshToken
            {
                Username = command.Username,
                Token = refreshTokenString,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };
            await _tokenRepository.SaveRefreshTokenAsync(refreshTokenEntity);

            return new AuthResult(accessToken, refreshTokenString);
        }

        throw new Exception("Invalid credentials");
    }
}