using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MyAuthSystem.Application.Common.Interfaces;
using MyAuthSystem.Infrastructure.Authentication;

namespace MyAuthSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {

        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        // ตั้งค่าระบบ Authentication และ JwtBearer
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "MyAuthSystem",
                ValidateAudience = true,
                ValidAudience = "MyAuthSystemUsers",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("SUPER_SECRET_KEY_THAT_IS_LONG_ENOUGH_32_BYTES"))
            };
        });

        // เปิดระบบตรวจสอบสิทธิ์
        services.AddAuthorization();

        return services;
    }
}