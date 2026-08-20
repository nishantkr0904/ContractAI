#pragma once

#include <cstddef>
#include <cstdint>
#include <optional>
#include <string_view>

namespace contractai {

// Structured values whose surface forms are open-ended, so they cannot be
// enumerated in the KeywordTrie the way a fixed legal vocabulary can.
enum class PatternKind : std::uint8_t {
    MonetaryAmount,
    Date,
    Duration,
    Percentage,
};

struct PatternMatch {
    std::size_t length;  // bytes of source text matched
    PatternKind kind;
};

// Longest pattern starting exactly at `pos`. A Date match spans the full range
// when the text reads as one ("January 1, 2026 through December 31, 2026").
// Callers should anchor `pos` at a token start, as KeywordTrie::LongestMatchAt does.
[[nodiscard]] std::optional<PatternMatch> LongestPatternAt(
    std::string_view text, std::size_t pos);

}  // namespace contractai
