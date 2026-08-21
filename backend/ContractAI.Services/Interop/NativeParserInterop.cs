using System.Runtime.InteropServices;

namespace ContractAI.Services.Interop;

// Raw bindings to the parser's extern "C" surface (parser/src/Interop.cpp). Kept
// internal so callers go through NativeClauseParser, which owns the input buffer's
// lifetime and turns these status codes into exceptions.
internal static partial class NativeParserInterop
{
    // No extension or prefix: the runtime applies the platform convention, so this
    // resolves libcontract_parser.dylib, libcontract_parser.so, or
    // contract_parser.dll. The file must sit next to the managed assembly.
    private const string Library = "contract_parser";

    internal const int Success = 0;
    internal const int InvalidArguments = -1;
    internal const int ExtractionFailed = -2;
    internal const int AllocationFailed = -3;

    // textBuffer is `ref byte` rather than a span or array so nothing is copied:
    // the source generator pins the reference for the duration of the call and
    // passes its address. Native treats it as const.
    //
    // Marshalling outClauses as a SafeHandle rather than an nint is what makes the
    // malloc'd array impossible to leak — the generator constructs the handle
    // before the call, so ownership is established the instant the pointer exists.
    [LibraryImport(Library, EntryPoint = "ParseContractClauses")]
    internal static partial int ParseContractClauses(
        ref byte textBuffer,
        nuint bufferLength,
        out ClauseOutputHandle outClauses,
        out nuint outCount);

    // Takes a raw nint, not the SafeHandle: this is called from the handle's own
    // ReleaseHandle, where wrapping the pointer again would recurse.
    [LibraryImport(Library, EntryPoint = "FreeClauseOutput")]
    internal static partial void FreeClauseOutput(nint clauses, nuint count);
}

// Mirrors the ClauseOutput C struct in parser/include/ClauseOutput.h. The struct
// holds no pointers, so it is blittable and marshals as a straight memory read.
//
// Sequential layout matches the C compiler's natural alignment here: two uint32 at
// 0 and 4, the double at 8 (its own alignment forces the slot), the byte at 16, and
// the whole thing padded to 24.
//
// Fields are public so the compiler does not flag them as never assigned; they are
// only ever written by native code.
[StructLayout(LayoutKind.Sequential)]
internal readonly struct ClauseOutput
{
    public readonly uint ByteOffset;
    public readonly uint ByteLength;
    public readonly double Confidence;
    public readonly byte Category;
}
