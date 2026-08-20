#include "ClauseExtractor.h"

namespace contractai {

namespace {
constexpr bool IsSpace(char c) {
    return c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f' || c == '\v';
}
}  // namespace

std::vector<Clause> ClauseExtractor::Extract([[maybe_unused]] std::string_view text) const {
    // Tokenization, Trie keyword matching, and regex boundary detection are
    // added incrementally; no boundaries are detected yet.
    return {};
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
