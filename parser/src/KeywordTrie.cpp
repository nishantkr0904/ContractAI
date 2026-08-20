#include "KeywordTrie.h"

namespace contractai {

namespace {

constexpr char ToLowerAscii(char c) {
    return (c >= 'A' && c <= 'Z') ? static_cast<char>(c - 'A' + 'a') : c;
}

// Characters that may not directly follow a match, so that indexed terms are
// not reported when they are merely a prefix of a longer word.
constexpr bool IsWordChar(char c) {
    return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
}

}  // namespace

std::string_view CategoryName(ClauseCategory category) {
    switch (category) {
        case ClauseCategory::Indemnification:       return "Indemnification";
        case ClauseCategory::PaymentTerms:          return "Payment Terms";
        case ClauseCategory::LimitationOfLiability: return "Limitation of Liability";
        case ClauseCategory::Termination:           return "Termination";
        case ClauseCategory::Confidentiality:       return "Confidentiality";
        case ClauseCategory::GoverningLaw:          return "Governing Law";
        case ClauseCategory::IntellectualProperty:  return "Intellectual Property";
        case ClauseCategory::Warranty:              return "Warranty";
        case ClauseCategory::ForceMajeure:          return "Force Majeure";
        case ClauseCategory::DisputeResolution:     return "Dispute Resolution";
        case ClauseCategory::Assignment:            return "Assignment";
        case ClauseCategory::DataProtection:        return "Data Protection";
        case ClauseCategory::AutoRenewal:           return "Auto-Renewal";
    }
    return {};
}

void KeywordTrie::Insert(std::string_view keyword, ClauseCategory category) {
    if (keyword.empty()) return;

    Node* node = &root_;
    for (char c : keyword) {
        auto& child = node->children[ToLowerAscii(c)];
        if (!child) child = std::make_unique<Node>();
        node = child.get();
    }
    node->terminal = category;
}

std::optional<KeywordMatch> KeywordTrie::LongestMatchAt(
    std::string_view text, std::size_t pos) const {
    const Node* node = &root_;
    std::optional<KeywordMatch> best;

    for (std::size_t k = 0; pos + k < text.size(); ++k) {
        const auto it = node->children.find(ToLowerAscii(text[pos + k]));
        if (it == node->children.end()) break;
        node = it->second.get();

        if (node->terminal) {
            const std::size_t end = pos + k + 1;
            if (end == text.size() || !IsWordChar(text[end])) {
                best = KeywordMatch{k + 1, *node->terminal};
            }
        }
    }
    return best;
}

KeywordTrie BuildLegalTermTrie() {
    struct Term {
        std::string_view text;
        ClauseCategory category;
    };

    static constexpr Term kTerms[] = {
        {"indemnify", ClauseCategory::Indemnification},
        {"indemnification", ClauseCategory::Indemnification},
        {"indemnity", ClauseCategory::Indemnification},
        {"hold harmless", ClauseCategory::Indemnification},

        {"payment terms", ClauseCategory::PaymentTerms},
        {"net 30", ClauseCategory::PaymentTerms},
        {"invoice", ClauseCategory::PaymentTerms},
        {"late fee", ClauseCategory::PaymentTerms},

        {"limitation of liability", ClauseCategory::LimitationOfLiability},
        {"liability", ClauseCategory::LimitationOfLiability},
        {"consequential damages", ClauseCategory::LimitationOfLiability},

        {"termination", ClauseCategory::Termination},
        {"terminate", ClauseCategory::Termination},
        {"for convenience", ClauseCategory::Termination},

        {"confidential", ClauseCategory::Confidentiality},
        {"confidentiality", ClauseCategory::Confidentiality},
        {"non-disclosure", ClauseCategory::Confidentiality},
        {"proprietary information", ClauseCategory::Confidentiality},

        {"governing law", ClauseCategory::GoverningLaw},
        {"jurisdiction", ClauseCategory::GoverningLaw},
        {"venue", ClauseCategory::GoverningLaw},

        {"intellectual property", ClauseCategory::IntellectualProperty},
        {"copyright", ClauseCategory::IntellectualProperty},
        {"patent", ClauseCategory::IntellectualProperty},
        {"trademark", ClauseCategory::IntellectualProperty},
        {"work product", ClauseCategory::IntellectualProperty},

        {"warranty", ClauseCategory::Warranty},
        {"warranties", ClauseCategory::Warranty},
        {"warrants", ClauseCategory::Warranty},

        {"force majeure", ClauseCategory::ForceMajeure},
        {"act of god", ClauseCategory::ForceMajeure},

        {"arbitration", ClauseCategory::DisputeResolution},
        {"mediation", ClauseCategory::DisputeResolution},
        {"dispute resolution", ClauseCategory::DisputeResolution},

        {"assignment", ClauseCategory::Assignment},

        {"personal data", ClauseCategory::DataProtection},
        {"data protection", ClauseCategory::DataProtection},
        {"gdpr", ClauseCategory::DataProtection},

        {"automatically renew", ClauseCategory::AutoRenewal},
        {"auto-renewal", ClauseCategory::AutoRenewal},
        {"renewal term", ClauseCategory::AutoRenewal},
    };

    KeywordTrie trie;
    for (const Term& term : kTerms) {
        trie.Insert(term.text, term.category);
    }
    return trie;
}

}  // namespace contractai
