namespace ContractAI.API.Auth;

// The claim and policy names the API agrees on with its token issuer. Kept as
// constants so controllers reference AuthPolicies.Writer rather than repeating
// the literal, and the scope strings match API_REFERENCE.md exactly.
public static class AuthClaims
{
    // The tenant the caller belongs to. Populated by the identity provider; the
    // API treats it as the sole source of tenant context.
    public const string TenantId = "tenant_id";

    // OAuth2 scope claim. May arrive space-delimited in one claim or split across
    // several; AuthSetup handles both shapes.
    public const string Scope = "scope";
}

public static class AuthScopes
{
    public const string Reader = "app_reader";
    public const string Writer = "app_writer";
}

public static class AuthPolicies
{
    public const string Reader = "RequireReader";
    public const string Writer = "RequireWriter";
}
