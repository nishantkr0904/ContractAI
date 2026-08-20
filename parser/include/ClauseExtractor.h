#pragma once

#include <cstddef>
#include <string_view>
#include <vector>

namespace contractai {

// A clause boundary within the source text, stored as an offset/length span
// rather than a copy to keep extraction zero-copy.
struct Clause {
    std::size_t offset;  // start byte offset into the source text
    std::size_t length;  // byte length of the span
};

class ClauseExtractor {
public:
    // Detects clause boundaries in `text`. Returned Clause spans reference the
    // caller's buffer, so they remain valid only as long as `text` does.
    [[nodiscard]] std::vector<Clause> Extract(std::string_view text) const;
};

// Splits `text` into whitespace-delimited tokens. Each token is a view into
// `text` (no content is copied), so every token shares `text`'s lifetime.
[[nodiscard]] std::vector<std::string_view> Tokenize(std::string_view text);

}  // namespace contractai
