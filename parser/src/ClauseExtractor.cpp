#include "ClauseExtractor.h"

namespace contractai {

std::vector<Clause> ClauseExtractor::Extract([[maybe_unused]] std::string_view text) const {
    // Tokenization, Trie keyword matching, and regex boundary detection are
    // added incrementally; no boundaries are detected yet.
    return {};
}

}  // namespace contractai
