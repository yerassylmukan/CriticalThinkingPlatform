using Microsoft.AspNetCore.Authorization;
using WebApi.Students.Api;
using WebApi.Students.Application;
using WebApi.Students.Security;

namespace WebApi.Students;

public static class StudentsModule
{
    public static void AddStudentsInfra(this WebApplicationBuilder b)
    {
        b.Services.AddAuthorization(o =>
        {
            o.AddPolicy("ClassOwner", p => p.RequireRole("Teacher").AddRequirements(new ClassOwnerRequirement()));
            o.AddPolicy("ClassMemberOrOwner", p => p.AddRequirements(new ClassMemberOrOwnerRequirement()));
        });

        b.Services.AddScoped<IAuthorizationHandler, ClassOwnerHandler>();
        b.Services.AddScoped<IAuthorizationHandler, ClassMemberOrOwnerHandler>();

        b.Services.AddScoped<StudentService>();
        b.Services.AddScoped<InviteService>();
        
        b.Services.Configure<InviteOptions>(o =>
        {
            b.Configuration.GetSection("JwtInvite").Bind(o);
            
            if (string.IsNullOrWhiteSpace(o.Hs256Secret))
            {
                o.Issuer   = b.Configuration["Jwt:Issuer"]    ?? "app-issuer";
                o.Audience = b.Configuration["Jwt:Audience"]  ?? "app-audience";
                o.Hs256Secret = b.Configuration["Jwt:Hs256Secret"]
                                ?? throw new InvalidOperationException("Missing JwtInvite:Hs256Secret or Jwt:Hs256Secret");
                o.Ttl = TimeSpan.FromDays(7);
            }
        });
    }

    public static void MapStudentsModule(this WebApplication app)
    {
        app.MapClasses();
        app.MapTeacherStudents();
        app.MapMe();
        app.MapInvitations();
    }
}