using System.Security.Claims;

namespace ContractAI.API.Auth;

// Resolves the caller's tenant from the validated JWT. Scoped so it reads the
// principal of the request in flight; controllers depend on it rather than
// digging claims out of HttpContext themselves, which keeps the "tenant comes
// from the token, never the URL" rule (API_REFERENCE.md) in exactly one place.
public interface ICurrentTenant
{
    Guid TenantId { get; }

    // The acting user, when the token carries a subject. Nullable because
    // uploaded_by / audit_logs.user_id are ON DELETE SET NULL and a token need not
    // identify a persisted user (e.g. a machine principal), so its absence is not
    // an error the way a missing tenant is.
    Guid? UserId { get; }
}

internal sealed class CurrentTenant(IHttpContextAccessor accessor) : ICurrentTenant
{
    public Guid TenantId
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirstValue(AuthClaims.TenantId);

            // A request that reached a controller has passed authentication, so the
            // claim must be present and well-formed; its absence is a server-side
            // misconfiguration of token issuance, not a client error.
            if (!Guid.TryParse(value, out var tenantId))
            {
                throw new InvalidOperationException(
                    $"Authenticated request is missing a valid '{AuthClaims.TenantId}' claim.");
            }

            return tenantId;
        }
    }

    public Guid? UserId
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            var value = principal?.FindFirstValue("sub")
                ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }
}
