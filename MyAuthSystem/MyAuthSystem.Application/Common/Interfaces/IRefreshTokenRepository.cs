using MyAuthSystem.Domain.Entities;

namespace MyAuthSystem.Application.Common.Interfaces;

public interface IRefreshTokenRepository
{
    Task SaveRefreshTokenAsync(UserRefreshToken token);
    Task<UserRefreshToken?> GetByTokenAsync(string token);
    Task UpdateRefreshTokenAsync(UserRefreshToken token);
    Task RevokeAllTokensForUserAsync(string username);
}