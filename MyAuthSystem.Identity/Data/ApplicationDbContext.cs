using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyAuthSystem.Identity.Models;

namespace MyAuthSystem.Identity.WebApi.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // ตาราง RefreshTokens
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ทำ Index ที่คอลัมน์ Token เพื่อให้ระบบค้นหาตั๋วได้เร็วขึ้น
        builder.Entity<RefreshToken>()
            .HasIndex(t => t.Token)
            .IsUnique();

    }
}