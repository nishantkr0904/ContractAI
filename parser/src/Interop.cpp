#include <cstdint>
#include <cstdlib>
#include <string_view>
#include <vector>

#include "ClauseExtractor.h"
#include "ClauseOutput.h"
#include "parser_export.h"

// The C-ABI surface bound by .NET P/Invoke. Everything here is extern "C" so the
// symbols are unmangled, and no C++ exception is allowed to escape: one crossing
// into the CLR is undefined behavior, so the boundary is firewalled with catch(...).
//
// Return codes:
//    0  success (outCount may be 0 when no clauses are found)
//   -1  invalid arguments
//   -2  extraction failed (a C++ exception was contained here)
//   -3  allocation failed
extern "C" {

PARSER_API int ParseContractClauses(
    const char* textBuffer,
    size_t bufferLength,
    ClauseOutput** outClauses,
    size_t* outCount) {
    if (outClauses == nullptr || outCount == nullptr) return -1;
    *outClauses = nullptr;
    *outCount = 0;
    if (textBuffer == nullptr && bufferLength != 0) return -1;

    std::vector<contractai::Clause> clauses;
    try {
        const contractai::ClauseExtractor extractor;
        clauses = extractor.Extract(std::string_view(textBuffer, bufferLength));
    } catch (...) {
        return -2;
    }

    if (clauses.empty()) return 0;

    auto* out = static_cast<ClauseOutput*>(std::malloc(clauses.size() * sizeof(ClauseOutput)));
    if (out == nullptr) return -3;

    for (size_t k = 0; k < clauses.size(); ++k) {
        out[k].byte_offset = static_cast<uint32_t>(clauses[k].offset);
        out[k].byte_length = static_cast<uint32_t>(clauses[k].length);
        out[k].confidence = clauses[k].confidence;
        out[k].category = static_cast<uint8_t>(clauses[k].category);
    }

    *outClauses = out;
    *outCount = clauses.size();
    return 0;
}

// Releases an array returned by ParseContractClauses. `count` is part of the
// documented ABI but unused here: ClauseOutput holds no interior pointers, so
// the whole block is reclaimed by a single free with no per-element walk.
// Passing null is safe, as free(nullptr) is a defined no-op.
PARSER_API void FreeClauseOutput(ClauseOutput* clauses, [[maybe_unused]] size_t count) {
    std::free(clauses);
}

}  // extern "C"
