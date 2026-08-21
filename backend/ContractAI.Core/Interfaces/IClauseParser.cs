using ContractAI.Core.Enums;

namespace ContractAI.Core.Interfaces;

// Extraction is CPU-bound and operates on an in-memory buffer, so the contract is
// synchronous: there is nothing to await, and an async signature would only add a
// state machine around a blocking call.
public interface IClauseParser
{
    // Throws ClauseParserException if the underlying engine fails.
    IReadOnlyList<ParsedClause> Parse(string text);
}

// ByteOffset and ByteLength locate the clause within the UTF-8 encoding of the
// parsed text, not within the UTF-16 string. That is the unit the native engine
// reports and the unit contract_clauses.byte_offset stores, so the two agree
// without a conversion step.
public sealed record ParsedClause(
    ClauseCategory Category,
    string Text,
    int ByteOffset,
    int ByteLength,
    double Confidence);
