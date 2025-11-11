using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebApi.Auth.Api;
using WebApi.Auth.Application;
using WebApi.Auth.Entities;
using WebApi.Auth.Infrastructure.Data;

namespace WebApi.Auth;

public static class AuthModule
{
    public static void AddAuthInfra(this WebApplicationBuilder b)
    {
        b.Services.AddDbContextPool<SchoolDbContext>(opt =>
            opt.UseNpgsql(b.Configuration.GetConnectionString("School")));

        b.Services
            .AddIdentityCore<ApplicationUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 6;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequireUppercase = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<SchoolDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        b.Services.Configure<JwtOptions>(b.Configuration.GetSection("Jwt"));

        b.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var sp = b.Services.BuildServiceProvider();
                var jwt = sp.GetRequiredService<IOptions<JwtOptions>>().Value;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt.Hs256Secret!))
                };
            });

        b.Services.AddAuthorization();
        b.Services.AddScoped<JwtTokenService>();
    }

    public static void MapAuthModule(this WebApplication app)
    {
        app.MapAuth();
    }
}