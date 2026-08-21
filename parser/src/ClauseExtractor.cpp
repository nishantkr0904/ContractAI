#include "ClauseExtractor.h"

#include <optional>

#include "PatternMatcher.h"

namespace contractai {

namespace {
constexpr bool IsSpace(char c) {
    return c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f' || c == '\v';
}

// A clause is delimited at sentence granularity; these terminators also cover
// the paragraph and list-item breaks common in contract prose.
constexpr bool IsSentenceEnd(char c) {
    return c == '.' || c == '!' || c == '?' || c == ';' || c == '\n';
}

// Heuristic confidence. A legal keyword is a moderate signal; a keyword that
// co-occurs with a structured value (a monetary cap, term length, or date)
// signals a substantive clause rather than an incidental mention. These are
// placeholders to be calibrated against ground-truth data in Phase 4.
constexpr double kKeywordConfidence = 0.75;
constexpr double kKeywordWithValueConfidence = 0.9;
}  // namespace

std::vector<Clause> ClauseExtractor::Extract(std::string_view text) const {
    std::vector<Clause> clauses;
    const std::size_t n = text.size();

    std::size_t i = 0;
    while (i < n) {
        std::size_t start = i;
        while (start < n && IsSpace(text[start])) ++start;
        if (start >= n) break;

        std::size_t end = start;
        while (end < n && !IsSentenceEnd(text[end])) ++end;
        i = (end < n) ? end + 1 : end;

        // Scan token starts within the sentence for the longest keyword match,
        // noting whether any structured value appears alongside it.
        std::optional<ClauseCategory> category;
        std::size_t best = 0;
        bool hasValue = false;
        for (std::size_t p = start; p < end;) {
            while (p < end && IsSpace(text[p])) ++p;
            if (p >= end) break;

            if (const auto m = trie_.LongestMatchAt(text, p); m && m->length > best) {
                best = m->length;
                category = m->category;
            }
            if (!hasValue && LongestPatternAt(text, p)) hasValue = true;

            while (p < end && !IsSpace(text[p])) ++p;
        }

        if (category) {
            std::size_t last = end;
            while (last > start && IsSpace(text[last - 1])) --last;
            clauses.push_back(Clause{
                start,
                last - start,
                *category,
                hasValue ? kKeywordWithValueConfidence : kKeywordConfidence,
            });
        }
    }
    return clauses;
}

std::vector<std::string_view> Tokenize(std::string_view text) {
    std::vector<std::string_view> tokens;
    std::size_t i = 0;
    while (i < text.size()) {
        while (i < text.size() && IsSpace(text[i])) ++i;
        if (i == text.size()) break;
        const std::size_t start = i;
        while (i < text.size() && !IsSpace(text[i])) ++i;
        tokens.push_back(text.substr(start, i - start));
    }
    return tokens;
}

}  // namespace contractai
