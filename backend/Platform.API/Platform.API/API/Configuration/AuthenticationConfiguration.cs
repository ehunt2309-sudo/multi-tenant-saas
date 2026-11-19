using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Platform.API.Infrastructure.Security;
using Platform.API.Shared.Constants;

namespace Platform.API.Api.Configuration;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddPlatformAuthentication(this IServiceCollection services)
    {
        // Fail at startup (not at first login) if the signing key is missing or too short.
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured lazily so the key can come from Key Vault / user-secrets.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((o, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                o.MapInboundClaims = false;          // keep "sub", "role", "tid" as-is
                o.SaveToken = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],   // blocks alg=none / algorithm confusion
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = CustomClaims.Subject,
                    RoleClaimType = CustomClaims.Role
                };
            });

        services.AddAuthorizationBuilder()
            // Secure by default: anything not explicitly [AllowAnonymous] needs a valid token.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.PlatformAdminOnly, p => p.RequireRole(Roles.PlatformAdmin))
            .AddPolicy(Policies.ManagerOnly, p => p.RequireRole(Roles.Manager))
            .AddPolicy(Policies.StaffOrManager, p => p.RequireRole(Roles.Staff, Roles.Manager))
            .AddPolicy(Policies.CustomerOnly, p => p.RequireRole(Roles.Customer))
            .AddPolicy(Policies.AnyTenantUser, p => p.RequireRole(Roles.Manager, Roles.Staff, Roles.Customer));

        return services;
    }
}
