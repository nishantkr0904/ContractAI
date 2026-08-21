using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ContractAI.API.Auth;

// Wires JWT bearer validation and the scope-based authorization policies. Isolated
// from Program.cs because the token-validation parameters and the scope-claim
// parsing are fiddly enough to be worth naming and testing on their own.
internal static class AuthSetup
{
    public static IServiceCollection AddContractAiAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Local dev validates against a symmetric key from config; a real
        // deployment swaps this for Authority/metadata discovery against the IdP.
        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(signingKey)),
                    // No inbound claim-type remapping, so tenant_id and scope reach
                    // the principal under the names the token actually used rather
                    // than the legacy SOAP URIs the handler maps by default.
                    NameClaimType = ClaimTypes.NameIdentifier,
                };
            });

        return services;
    }

    public static IServiceCollection AddContractAiAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Writer implies reader: API_REFERENCE.md grants read/write to
            // app_writer, so a writer token must satisfy read-only endpoints too.
            options.AddPolicy(AuthPolicies.Reader, policy =>
                policy.RequireAssertion(ctx =>
                    ctx.User.HasScope(AuthScopes.Reader) ||
                    ctx.User.HasScope(AuthScopes.Writer)));

            options.AddPolicy(AuthPolicies.Writer, policy =>
                policy.RequireAssertion(ctx => ctx.User.HasScope(AuthScopes.Writer)));
        });

        return services;
    }

    // Accepts either a single space-delimited "scope" claim (the OAuth2 norm) or
    // several separate scope claims, so the check does not depend on how the issuer
    // chose to serialize them.
    private static bool HasScope(this ClaimsPrincipal user, string scope) =>
        user.FindAll(AuthClaims.Scope)
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope);
}
