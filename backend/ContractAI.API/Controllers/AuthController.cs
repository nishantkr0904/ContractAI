using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ContractAI.API.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace ContractAI.API.Controllers;

// Development-only token minting. There is no real IdP in local dev, so this hands
// the SPA a signed JWT the same way the checked-in Python minter does — same key,
// issuer, audience, and claim shape the JwtBearer handler already validates. Every
// action first checks the environment and 404s outside Development, so the route
// does not exist in a deployed build even though the controller ships in the image.
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IConfiguration configuration, IWebHostEnvironment environment)
    : ControllerBase
{
    // Fixed dev principal, matching the seeded tenant/user and the Python minter so
    // tokens from either path resolve to the same data.
    private const string DevTenantId = "11111111-1111-1111-1111-111111111111";
    private const string DevUserId = "22222222-2222-2222-2222-222222222222";
    private static readonly TimeSpan DevTokenLifetime = TimeSpan.FromHours(8);

    [HttpPost("dev-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(DevTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult IssueDevToken([FromBody] DevTokenRequest request)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        // Default to the least-privileged scope; only "writer" unlocks writes. An
        // unrecognized role is a client error rather than a silent downgrade.
        var scope = (request.Role ?? "reader").Trim().ToLowerInvariant() switch
        {
            "reader" => AuthScopes.Reader,
            "writer" => AuthScopes.Writer,
            _ => null,
        };

        if (scope is null)
        {
            ModelState.AddModelError("role", "role must be 'reader' or 'writer'.");
            return ValidationProblem(ModelState);
        }

        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var expires = DateTime.UtcNow.Add(DevTokenLifetime);
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, DevUserId),
                new Claim(AuthClaims.TenantId, DevTenantId),
                new Claim(AuthClaims.Scope, scope),
            ],
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(new DevTokenResponse(encoded, "Bearer", expires, scope, DevTenantId));
    }
}

public sealed record DevTokenRequest(string? Role);

public sealed record DevTokenResponse(
    string Token,
    string TokenType,
    DateTimeOffset ExpiresAt,
    string Scope,
    string TenantId);
