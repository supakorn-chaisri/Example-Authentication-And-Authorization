using MyAuthSystem.Identity.Models;

namespace MyAuthSystem.Identity.Services;

public interface ITokenService
{
    Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(ApplicationUser user);
    Task<(string AccessToken, string RefreshToken)?> VerifyAndRotateTokenAsync(TokenRequestDto model);
    Task<bool> RevokeRefreshTokenAsync(string refreshToken);
}