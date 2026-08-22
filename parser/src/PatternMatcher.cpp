#include "PatternMatcher.h"

#include <array>

#include <re2/re2.h>

namespace contractai {

namespace {

struct Pattern {
    const RE2& re;
    PatternKind kind;
};

// One compiled RE2 per kind, built once on first use. RE2 is thread-safe for
// const matching, so a single static instance serves concurrent P/Invoke calls.
// longest_match gives POSIX leftmost-longest semantics, so an alternation such
// as the optional date-range tail is taken whenever it can extend the match.
const std::array<Pattern, 4>& Patterns() {
    static const RE2::Options kOpts = [] {
        RE2::Options o;
        o.set_longest_match(true);
        return o;
    }();

    // A single calendar date in ISO, US-numeric, or long month-name form.
    static const std::string kDate =
        R"((?:[0-9]{4}-[0-9]{2}-[0-9]{2}|[0-9]{1,2}/[0-9]{1,2}/[0-9]{2,4}|)"
        R"((?:january|february|march|april|may|june|july|august|september|)"
        R"(october|november|december)\s+[0-9]{1,2},?\s+[0-9]{4}))";

    static const RE2 kMoney(
        R"((?i)(?:\$\s?[0-9][0-9,]*(?:\.[0-9]{2})?|)"
        R"((?:usd|eur|gbp)\s?[0-9][0-9,]*(?:\.[0-9]{1,2})?))"
        R"((?:\s+(?:million|billion|thousand)\b)?)",
        kOpts);

    // A single date, optionally followed by a range tail ("... through <date>").
    static const RE2 kDateRe(
        "(?i)" + kDate + R"((?:\s+(?:through|thru|until|to)\s+)" + kDate + ")?",
        kOpts);

    static const RE2 kDuration(
        R"((?i)(?:[0-9]+|[a-z]+\s*\([0-9]+\))[\s-]+)"
        R"((?:calendar\s+|business\s+)?(?:days?|weeks?|months?|years?)\b)",
        kOpts);

    static const RE2 kPercent(R"((?i)[0-9]+(?:\.[0-9]+)?\s?(?:%|percent\b))", kOpts);

    static const std::array<Pattern, 4> kPatterns{{
        {kMoney, PatternKind::MonetaryAmount},
        {kDateRe, PatternKind::Date},
        {kDuration, PatternKind::Duration},
        {kPercent, PatternKind::Percentage},
    }};
    return kPatterns;
}

}  // namespace

std::optional<PatternMatch> LongestPatternAt(std::string_view text, std::size_t pos) {
    if (pos >= text.size()) return std::nullopt;

    std::optional<PatternMatch> best;
    for (const Pattern& p : Patterns()) {
        re2::StringPiece match;
        if (p.re.Match(text, pos, text.size(), RE2::ANCHOR_START, &match, 1) &&
            !match.empty() && (!best || match.size() > best->length)) {
            best = PatternMatch{match.size(), p.kind};
        }
    }
    return best;
}

}  // namespace contractai
