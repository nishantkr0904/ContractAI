#include <cstddef>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <string_view>
#include <utility>

#include "ClauseExtractor.h"
#include "ClauseOutput.h"
#include "KeywordTrie.h"
#include "PatternMatcher.h"
#include "parser_export.h"

extern "C" PARSER_API int ParseContractClauses(const char*, size_t, ClauseOutput**, size_t*);
extern "C" PARSER_API void FreeClauseOutput(ClauseOutput*, size_t);

namespace {

using namespace contractai;

int g_failures = 0;

// assert() expands to nothing under NDEBUG, which Release builds define, so the
// suite would silently pass. This reports every failure and drives the exit code.
void Report(bool ok, const char* expr, int line) {
    if (!ok) {
        std::fprintf(stderr, "FAIL line %d: %s\n", line, expr);
        ++g_failures;
    }
}

#define CHECK(expr) Report(static_cast<bool>(expr), #expr, __LINE__)

void TestTokenize() {
    constexpr std::string_view text = "  Termination\tfor\nCONVENIENCE   and  liability. ";
    const auto tokens = Tokenize(text);
    CHECK(tokens.size() == 5);
    CHECK(tokens[0] == "Termination");
    CHECK(tokens[2] == "CONVENIENCE");
    CHECK(tokens[4] == "liability.");

    // Zero-copy: every token must view the source buffer rather than a copy.
    for (const std::string_view token : tokens) {
        CHECK(token.data() >= text.data());
        CHECK(token.data() + token.size() <= text.data() + text.size());
    }

    CHECK(Tokenize("").empty());
    CHECK(Tokenize("   \t\n ").empty());
}

void TestTrie() {
    const KeywordTrie trie = BuildLegalTermTrie();

    // The longest indexed term wins over a shorter one sharing its prefix.
    const auto indemnity = trie.LongestMatchAt("indemnification obligations", 0);
    CHECK(indemnity && indemnity->length == 15);
    CHECK(indemnity && indemnity->category == ClauseCategory::Indemnification);

    // Trailing punctuation still closes a match.
    const auto liability = trie.LongestMatchAt("liability.", 0);
    CHECK(liability && liability->length == 9);

    // Word-boundary guard: a term must not fire inside a longer word.
    CHECK(!trie.LongestMatchAt("invoiced", 0));
    CHECK(trie.LongestMatchAt("invoice.", 0));

    // Multi-word terms match case-insensitively across the space.
    const auto majeure = trie.LongestMatchAt("FORCE MAJEURE event", 0);
    CHECK(majeure && majeure->length == 13);
    CHECK(majeure && majeure->category == ClauseCategory::ForceMajeure);

    CHECK(!trie.LongestMatchAt("hello", 0));

    // Labels are the clause_types names pinned in docs/API_REFERENCE.md.
    CHECK(CategoryName(ClauseCategory::Indemnification) == "Indemnification");
    CHECK(CategoryName(ClauseCategory::PaymentTerms) == "Payment Terms");
}

void TestPatterns() {
    const auto money = LongestPatternAt("$1,000,000.00 cap", 0);
    CHECK(money && money->length == 13 && money->kind == PatternKind::MonetaryAmount);

    const auto scaled = LongestPatternAt("$5 million total", 0);
    CHECK(scaled && scaled->length == 10);

    const auto percent = LongestPatternAt("10.5 percent annually", 0);
    CHECK(percent && percent->length == 12 && percent->kind == PatternKind::Percentage);

    const auto duration = LongestPatternAt("60-day cure period", 0);
    CHECK(duration && duration->length == 6 && duration->kind == PatternKind::Duration);

    const auto spelled = LongestPatternAt("one (1) year term", 0);
    CHECK(spelled && spelled->length == 12 && spelled->kind == PatternKind::Duration);

    // A range must extend across the connector instead of stopping at the first date.
    const auto range = LongestPatternAt("January 1, 2026 through December 31, 2026.", 0);
    CHECK(range && range->length == 41 && range->kind == PatternKind::Date);
    CHECK(LongestPatternAt("2026-01-15 effective", 0));

    // Word-boundary guards, and no false positives on plain prose.
    CHECK(!LongestPatternAt("5percentile", 0));
    CHECK(!LongestPatternAt("30 dayspring", 0));
    CHECK(!LongestPatternAt("the parties hereby agree", 0));
}

void TestExtract() {
    constexpr std::string_view text =
        "The Vendor shall indemnify and hold harmless the Client against all claims. "
        "Payment terms are net 30 with a late fee of 5% per month. "
        "The parties agree to cooperate in good faith.";

    const ClauseExtractor extractor;
    const auto clauses = extractor.Extract(text);

    // The closing sentence carries no legal keyword, so it is not a clause.
    CHECK(clauses.size() == 2);
    if (clauses.size() == 2) {
        CHECK(clauses[0].category == ClauseCategory::Indemnification);
        CHECK(clauses[0].confidence == 0.75);  // keyword only

        // Category is the longest keyword in the sentence: "payment terms" over "net 30".
        CHECK(clauses[1].category == ClauseCategory::PaymentTerms);
        CHECK(clauses[1].confidence == 0.9);  // the "5%" value co-occurs

        const std::string_view first = text.substr(clauses[0].offset, clauses[0].length);
        CHECK(first.find("indemnify") != std::string_view::npos);
        CHECK(!first.empty() && first.back() != ' ');  // spans are trimmed
        CHECK(clauses[0].offset + clauses[0].length <= text.size());
    }

    CHECK(extractor.Extract("").empty());
    CHECK(extractor.Extract("Nothing of legal note here.").empty());
}

void TestAbi() {
    const char* text =
        "The Vendor shall indemnify the Client. "
        "Payment terms are net 30 with a 5% late fee. "
        "This Agreement shall automatically renew for 12 months.";
    const size_t length = std::strlen(text);
    const std::string_view view(text, length);

    ClauseOutput* clauses = nullptr;
    size_t count = 0;
    CHECK(ParseContractClauses(text, length, &clauses, &count) == 0);
    CHECK(count == 3);
    CHECK(clauses != nullptr);

    if (clauses != nullptr && count == 3) {
        for (size_t k = 0; k < count; ++k) {
            CHECK(clauses[k].byte_offset + clauses[k].byte_length <= length);
            CHECK(clauses[k].confidence >= 0.0 && clauses[k].confidence <= 1.0);
        }
        CHECK(clauses[0].category == static_cast<uint8_t>(ClauseCategory::Indemnification));
        CHECK(clauses[2].category == static_cast<uint8_t>(ClauseCategory::AutoRenewal));
        const std::string_view second =
            view.substr(clauses[1].byte_offset, clauses[1].byte_length);
        CHECK(second.find("net 30") != std::string_view::npos);
    }
    FreeClauseOutput(clauses, count);

    // Out-params are reset even when nothing matches, so callers may free blindly.
    clauses = reinterpret_cast<ClauseOutput*>(0x1);
    count = 99;
    CHECK(ParseContractClauses("", 0, &clauses, &count) == 0);
    CHECK(clauses == nullptr);
    CHECK(count == 0);
    FreeClauseOutput(clauses, count);  // null release is a no-op

    // Invalid arguments are rejected rather than dereferenced.
    CHECK(ParseContractClauses(text, length, nullptr, &count) == -1);
    CHECK(ParseContractClauses(text, length, &clauses, nullptr) == -1);
    CHECK(ParseContractClauses(nullptr, 10, &clauses, &count) == -1);
}

constexpr std::pair<std::string_view, void (*)()> kSuites[] = {
    {"tokenize", TestTokenize},
    {"trie", TestTrie},
    {"patterns", TestPatterns},
    {"extract", TestExtract},
    {"abi", TestAbi},
};

}  // namespace

// With no argument every suite runs; CTest registers one entry per suite name so
// a failure points at the area rather than the whole binary.
int main(int argc, char** argv) {
    const std::string_view selected = (argc > 1) ? argv[1] : std::string_view{};

    bool ran = false;
    for (const auto& [name, run] : kSuites) {
        if (selected.empty() || selected == name) {
            run();
            ran = true;
        }
    }

    if (!ran) {
        std::fprintf(stderr, "unknown suite: %.*s\n",
                     static_cast<int>(selected.size()), selected.data());
        return 2;
    }
    if (g_failures != 0) {
        std::fprintf(stderr, "%d check(s) failed\n", g_failures);
        return 1;
    }
    std::puts("OK");
    return 0;
}
