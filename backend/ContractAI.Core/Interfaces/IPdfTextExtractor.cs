namespace ContractAI.Core.Interfaces;

// Extracts a flat text buffer from a PDF and the page boundaries within it. The
// native parser works on the flat buffer and reports byte offsets into it; the
// boundaries are what let the service turn those offsets back into page numbers.
public interface IPdfTextExtractor
{
    ExtractedText Extract(Stream pdf);
}

// Text is the concatenation of every page's text. PageStartByteOffsets[i] is the
// UTF-8 byte offset in Text where page i (0-based) begins, so a clause at byte
// offset N sits on the last page whose start offset is <= N. The offsets are in
// UTF-8 because that is the encoding the parser is handed and the unit
// contract_clauses.byte_offset stores.
public sealed record ExtractedText(string Text, IReadOnlyList<int> PageStartByteOffsets)
{
    // Resolves a UTF-8 byte offset to a 1-based page number: the page is the one
    // whose start offset is the greatest that does not exceed the offset.
    public int PageNumberForByteOffset(int byteOffset)
    {
        var low = 0;
        var high = PageStartByteOffsets.Count - 1;
        var page = 0;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            if (PageStartByteOffsets[mid] <= byteOffset)
            {
                page = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return page + 1;
    }
}
