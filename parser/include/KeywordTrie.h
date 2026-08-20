#pragma once

#include <cstddef>
#include <cstdint>
#include <memory>
#include <optional>
#include <string_view>
#include <unordered_map>

namespace contractai {

// Clause taxonomy. CategoryName() returns the canonical label, which is the
// value expected in the clause_types lookup table.
enum class ClauseCategory : std::uint8_t {
    Indemnification,
    PaymentTerms,
    LimitationOfLiability,
    Termination,
    Confidentiality,
    GoverningLaw,
    IntellectualProperty,
    Warranty,
    ForceMajeure,
    DisputeResolution,
    Assignment,
    DataProtection,
    AutoRenewal,
};

[[nodiscard]] std::string_view CategoryName(ClauseCategory category);

struct KeywordMatch {
    std::size_t length;  // bytes of source text matched
    ClauseCategory category;
};

// Prefix tree over legal terms. A lookup costs O(L) in the length of the term
// being matched, independent of how many terms are indexed.
class KeywordTrie {
public:
    // `keyword` is indexed case-insensitively and may contain spaces, so
    // multi-word terms such as "force majeure" are supported.
    void Insert(std::string_view keyword, ClauseCategory category);

    // Longest indexed term starting exactly at `pos`. A match is only reported
    // when it ends on a word boundary, so "invoice" will not match inside
    // "invoiced". Callers should anchor `pos` at a token start.
    [[nodiscard]] std::optional<KeywordMatch> LongestMatchAt(
        std::string_view text, std::size_t pos) const;

private:
    struct Node {
        std::unordered_map<char, std::unique_ptr<Node>> children;
        std::optional<ClauseCategory> terminal;
    };

    Node root_;
};

// Trie seeded with the default commercial-contract legal terms.
[[nodiscard]] KeywordTrie BuildLegalTermTrie();

}  // namespace contractai
