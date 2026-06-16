using Microsoft.EntityFrameworkCore;
using MyAuthSystem.Application.Common.Interfaces;
using MyAuthSystem.Domain.Entities;
using MyAuthSystem.Infrastructure.Data;

namespace MyAuthSystem.Infrastructure.Persistence;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly MyDbContext _context;

    public RefreshTokenRepository(MyDbContext context) => _context = context;


    public async Task SaveRefreshTokenAsync(UserRefreshToken token)
    {
        await _context.UserRefreshTokens.AddAsync(token);
        await _context.SaveChangesAsync();
    }

    public async Task<UserRefreshToken?> GetByTokenAsync(string token)
    {
        return await _context.UserRefreshTokens
            .FirstOrDefaultAsync(t => t.Token == token);
    }

    public async Task UpdateRefreshTokenAsync(UserRefreshToken token)
    {
        _context.UserRefreshTokens.Update(token);
        await _context.SaveChangesAsync();
    }

    public async Task RevokeAllTokensForUserAsync(string username)
    {
        await _context.UserRefreshTokens
            .Where(t => t.Username == username && !t.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true));
    }
}