using System.Runtime.InteropServices;

namespace ContractAI.Services.Interop;

// Owns the ClauseOutput array that ParseContractClauses allocates with malloc, so
// it is released exactly once even if the caller throws while reading it.
// SafeHandle also guarantees the release runs at most once, which rules out the
// double-free that a plain try/finally around a raw pointer invites.
internal sealed class ClauseOutputHandle : SafeHandle
{
    // Required by the [LibraryImport] source generator: it constructs the instance
    // before invoking the native function and calls SetHandle after it returns.
    public ClauseOutputHandle() : base(nint.Zero, ownsHandle: true)
    {
    }

    // A successful parse of a document containing no clauses yields a null pointer
    // with a zero count. That is not an error, so this only reports null, and
    // callers distinguish the two cases using the out-count.
    public override bool IsInvalid => handle == nint.Zero;

    // Assigned by the caller as soon as the out-count is known, because the
    // generator gives us no way to pass it through the constructor. FreeClauseOutput
    // documents count as unused today — ClauseOutput has no interior pointers, so a
    // single free suffices — but it is part of the ABI, so the real value is kept.
    internal nuint Count { get; set; }

    protected override bool ReleaseHandle()
    {
        NativeParserInterop.FreeClauseOutput(handle, Count);
        return true;
    }
}
