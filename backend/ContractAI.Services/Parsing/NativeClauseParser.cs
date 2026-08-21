using System.Runtime.InteropServices;
using System.Text;
using ContractAI.Core.Enums;
using ContractAI.Core.Interfaces;
using ContractAI.Services.Interop;

namespace ContractAI.Services.Parsing;

// Managed face of the C++ clause engine. Everything unsafe about the boundary is
// contained here: buffer pinning, the status-code contract, and validating what
// native hands back before it is trusted as an index into managed memory.
public sealed class NativeClauseParser : IClauseParser
{
    public IReadOnlyList<ParsedClause> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Short-circuited so the pinned reference below is never taken on an empty
        // array, where it would point just past the object header rather than at a
        // valid byte. Native would accept it, but the intent is clearer this way.
        if (text.Length == 0)
        {
            return [];
        }

        // The engine reports offsets into the exact buffer it was given, so clause
        // text has to be sliced from these same UTF-8 bytes. Indexing the original
        // string instead would use UTF-16 positions and drift on any non-ASCII input.
        var utf8 = Encoding.UTF8.GetBytes(text);

        var status = NativeParserInterop.ParseContractClauses(
            ref MemoryMarshal.GetArrayDataReference(utf8),
            (nuint)utf8.Length,
            out var handle,
            out var count);

        using (handle)
        {
            // Before any early return, so a failure path still frees with the count
            // the ABI expects.
            handle.Count = count;

            if (status != NativeParserInterop.Success)
            {
                throw new ClauseParserException(DescribeFailure(status));
            }

            if (count == 0 || handle.IsInvalid)
            {
                return [];
            }

            return Materialize(handle, count, utf8);
        }
    }

    private static List<ParsedClause> Materialize(ClauseOutputHandle handle, nuint count, byte[] utf8)
    {
        ReadOnlySpan<ClauseOutput> native;
        unsafe
        {
            // Reading through the raw pointer without the DangerousAddRef dance is
            // sound here: the handle is a live local inside the caller's using block,
            // so it cannot be disposed or finalized while this span is in use.
            native = new ReadOnlySpan<ClauseOutput>(
                (void*)handle.DangerousGetHandle(),
                checked((int)count));
        }

        var clauses = new List<ParsedClause>(native.Length);

        for (var i = 0; i < native.Length; i++)
        {
            ref readonly var clause = ref native[i];

            // Native is a trust boundary, and these values become offsets into a
            // managed array, so they are range-checked rather than assumed good. The
            // arithmetic is widened to long because both fields are uint and could
            // otherwise wrap when summed.
            if ((long)clause.ByteOffset + clause.ByteLength > utf8.Length)
            {
                throw new ClauseParserException(
                    $"Parser returned clause {i} spanning bytes " +
                    $"{clause.ByteOffset}..{(long)clause.ByteOffset + clause.ByteLength} " +
                    $"of a {utf8.Length}-byte buffer.");
            }

            var category = (ClauseCategory)clause.Category;
            if (!Enum.IsDefined(category))
            {
                throw new ClauseParserException(
                    $"Parser returned unknown clause category ordinal {clause.Category}. " +
                    "ClauseCategory is out of sync with contractai::ClauseCategory.");
            }

            clauses.Add(new ParsedClause(
                category,
                Encoding.UTF8.GetString(utf8, (int)clause.ByteOffset, (int)clause.ByteLength),
                (int)clause.ByteOffset,
                (int)clause.ByteLength,
                clause.Confidence));
        }

        return clauses;
    }

    private static string DescribeFailure(int status) => status switch
    {
        NativeParserInterop.InvalidArguments =>
            "Clause parser rejected the arguments it was given.",
        NativeParserInterop.ExtractionFailed =>
            "Clause extraction failed inside the native engine.",
        NativeParserInterop.AllocationFailed =>
            "Clause parser could not allocate its result array.",
        _ => $"Clause parser returned unrecognized status {status}.",
    };
}
