using Microsoft.EntityFrameworkCore;
using MyAuthSystem.Domain.Entities; // 💎 ดึงคลาสมาจากชั้น Domain

namespace MyAuthSystem.Infrastructure.Data;

public class MyDbContext : DbContext
{
    public MyDbContext(DbContextOptions<MyDbContext> options) : base(options)
    {
    }

    public DbSet<UserRefreshToken> UserRefreshTokens => Set<UserRefreshToken>();
}