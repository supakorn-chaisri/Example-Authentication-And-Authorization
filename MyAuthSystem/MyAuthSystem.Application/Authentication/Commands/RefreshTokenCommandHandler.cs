using MyAuthSystem.Application.Common.Interfaces;
using MyAuthSystem.Domain.Entities;

namespace MyAuthSystem.Application.Authentication.Commands;

public class RefreshTokenCommandHandler
{
    private readonly IRefreshTokenRepository _tokenRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public RefreshTokenCommandHandler(IRefreshTokenRepository tokenRepository, IJwtTokenGenerator jwtTokenGenerator)
    {
        _tokenRepository = tokenRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<AuthResult> HandleAsync(RefreshTokenCommand command)
    {
        // ค้นหา Refresh Token ใน db
        var storedToken = await _tokenRepository.GetByTokenAsync(command.RefreshToken);

        if (storedToken == null || storedToken.ExpiresAt < DateTime.UtcNow)
        {
            throw new Exception("Invalid or expired refresh token");
        }

        if (storedToken.IsRevoked)
        {
            // ทำลายตั๋วทั้งหมดของ User คนนี้ทันทีเพื่อความปลอดภัย (Lockdown)
            await _tokenRepository.RevokeAllTokensForUserAsync(storedToken.Username);
            throw new Exception("Security breach detected! All sessions have been terminated. Please login again.");
        }

        // ออกตั๋วชุดใหม่ (Access Token และ Refresh Token ใบใหม่)
        var newAccessToken = _jwtTokenGenerator.GenerateToken(
            userId: "1",
            username: storedToken.Username,
            role: "Admin"
        );

        var newRefreshTokenString = Guid.NewGuid().ToString();

        // ทำทำลายตั๋วเก่า (Revoke) ตามหลัก Refresh Token Rotation
        storedToken.IsRevoked = true;
        await _tokenRepository.UpdateRefreshTokenAsync(storedToken);

        // บันทึกตั๋ว Refresh Token ใบใหม่ลง db
        var newRefreshTokenEntity = new UserRefreshToken
        {
            Username = storedToken.Username,
            Token = newRefreshTokenString,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        await _tokenRepository.SaveRefreshTokenAsync(newRefreshTokenEntity);

        return new AuthResult(newAccessToken, newRefreshTokenString);
    }
}

// DTO แบบง่ายสำหรับส่งผลลัพธ์กลับไปให้ Controller
public record AuthResult(string AccessToken, string RefreshToken);