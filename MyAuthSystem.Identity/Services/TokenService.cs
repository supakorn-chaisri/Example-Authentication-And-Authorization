using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyAuthSystem.Identity.Models;
using MyAuthSystem.Identity.WebApi.Data;

namespace MyAuthSystem.Identity.Services;

public class TokenService
{
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _dbContext;

    public TokenService(IConfiguration configuration, ApplicationDbContext dbContext)
    {
        _configuration = configuration;
        _dbContext = dbContext;
    }

    public async Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(ApplicationUser user)
    {
        // 1. ดึงค่าคอนฟิกมาจาก AppSettings และ User Secrets
        var secretKey = _configuration["JwtSettings:Secret"] ?? throw new InvalidOperationException("Secret key is missing.");
        var issuer = _configuration["JwtSettings:Issuer"];
        var audience = _configuration["JwtSettings:Audience"];
        var expiryMinutes = double.Parse(_configuration["JwtSettings:AccessTokenExpirationInMinutes"] ?? "15");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwtId = Guid.NewGuid().ToString();

        // 2. บรรจุ Claims (ข้อมูลดิบของผู้ใช้) เข้าไปในตั๋วพาร์ท Payload
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim("given_name", user.FirstName),
            new Claim("family_name", user.LastName),
            new Claim(JwtRegisteredClaimNames.Jti, jwtId) // ไอดีอ้างอิงของตั๋วใบนี้
        };

        // 3. เนรมิตตั๋ว JWT ขึ้นมา
        var token = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(token);
        var accessToken = tokenHandler.WriteToken(securityToken);

        // --- เริ่มพาร์ทบันทึก Refresh Token ลง PostgreSQL ---
        var rawRefreshToken = GenerateRawTokenString();

        var refreshTokenEntity = new RefreshToken
        {
            Token = rawRefreshToken,
            JwtId = jwtId,
            UserId = user.Id,
            IsUsed = false,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7) // กำหนดอายุ Refresh Token ยาว 7 วันตามมาตรฐาน
        };

        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _dbContext.SaveChangesAsync(); // บันทึกลง Postgres

        return (accessToken, rawRefreshToken);
    }

    public async Task<(string AccessToken, string RefreshToken)?> VerifyAndRotateTokenAsync(TokenRequestDto model)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var secretKey = _configuration["JwtSettings:Secret"] ?? throw new InvalidOperationException("Secret key is missing.");

        // ตรวจสอบโครงสร้างของ Access Token และดึงค่า Claims เดิมออกมา
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = _configuration["JwtSettings:Issuer"],
            ValidateAudience = true,
            ValidAudience = _configuration["JwtSettings:Audience"],
            ValidateLifetime = false // ปิดการตรวจเวลา เพราะเราตั้งใจรับโทเค็นที่หมดอายุมาแกะดู
        };

        SecurityToken securityToken;
        var principal = tokenHandler.ValidateToken(model.AccessToken, tokenValidationParameters, out securityToken);

        // ตรวจสอบว่าเป็นอัลกอริทึม HMAC SHA256 จริงไหมเพื่อความปลอดภัย
        if (securityToken is not JwtSecurityToken jwtSecurityToken ||
            !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
        {
            return null;
        }

        // ดึงค่า JTI (Jwt ID) และ User ID จาก Access Token เดิม
        var jwtId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value; // ในระบบ Identity คือค่า sub

        // ค้นหา Refresh Token ใน db ที่ส่งคู่กันมา
        var storedRefreshToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == model.RefreshToken);

        if (storedRefreshToken == null) return null;

        // ตรวจสอบการนำ Refresh Token เก่ามาใช้ซ้ำ!
        if (storedRefreshToken.IsUsed || storedRefreshToken.IsRevoked)
        {
            var allUserTokens = await _dbContext.RefreshTokens
                .Where(t => t.UserId == storedRefreshToken.UserId)
                .ToListAsync();

            foreach (var t in allUserTokens)
            {
                t.IsRevoked = true;
            }
            await _dbContext.SaveChangesAsync();
            return null;
        }

        if (storedRefreshToken.JwtId != jwtId) return null; // ตั๋วไม่แมตช์คู่กัน
        if (storedRefreshToken.ExpiresAt < DateTime.UtcNow) return null; // ตั๋วหมดอายุแล้ว

        storedRefreshToken.IsUsed = true; // มาร์คว่าใบเก่าถูกใช้ไปแล้ว
        await _dbContext.SaveChangesAsync();

        var user = await _dbContext.Users.FindAsync(storedRefreshToken.UserId);
        if (user == null) return null;

        return await GenerateTokensAsync(user);
    }

    public async Task<bool> RevokeRefreshTokenAsync(string refreshToken)
    {
        var tokenInDb = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);
        if (tokenInDb == null) return false;

        tokenInDb.IsRevoked = true; // สั่งแบนตั๋วใบนี้
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public string GenerateRawTokenString()
    {
        var randomNumber = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}