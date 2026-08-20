#pragma once

#include <stdint.h>

// One extracted clause, as it crosses the C-ABI into .NET.
//
// Deliberately holds no pointers: the text is identified by an offset/length
// pair into the caller's own buffer instead of a copied string. That keeps the
// struct blittable, so .NET marshals nothing, and makes releasing the array a
// single free that cannot leave dangling interior pointers behind.
//
// page_number is absent because the parser is handed a flat text buffer and has
// no view of PDF pagination; the managed side resolves byte_offset to a page
// against the offset table it built while extracting the text.
typedef struct ClauseOutput {
    uint32_t byte_offset;  // start of the clause within the input buffer
    uint32_t byte_length;  // byte length of the clause span
    double confidence;     // extraction certainty, 0.0 to 1.0
    uint8_t category;      // ClauseCategory ordinal, see KeywordTrie.h
} ClauseOutput;
