using System.Text;
using ContractAI.Core.Interfaces;
using UglyToad.PdfPig;

namespace ContractAI.Services.Parsing;

// PdfPig-backed text extraction. Concatenates page text with a single newline
// separator and records, for each page, the UTF-8 byte offset at which it begins
// so the analysis service can map clause offsets back to page numbers.
//
// This fork of PdfPig omits the DocumentLayoutAnalysis extractors, so page text is
// rebuilt from the spatially-grouped words joined by spaces rather than
// ContentOrderTextExtractor. Extraction quality is not load-bearing for offset
// correctness: page-start offsets are computed against this same string, and the
// parser reports offsets into the exact buffer produced here.
public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    private const char PageSeparator = '\n';

    public ExtractedText Extract(Stream pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        var text = new StringBuilder();
        var pageStartByteOffsets = new List<int>();

        // Byte length, not char length: offsets are UTF-8 to match what the native
        // parser reports and what the database column stores.
        var byteOffset = 0;

        using var document = PdfDocument.Open(pdf);
        foreach (var page in document.GetPages())
        {
            pageStartByteOffsets.Add(byteOffset);

            var pageText = string.Join(' ', page.GetWords().Select(word => word.Text));
            text.Append(pageText);
            byteOffset += Encoding.UTF8.GetByteCount(pageText);

            text.Append(PageSeparator);
            byteOffset += 1;
        }

        return new ExtractedText(text.ToString(), pageStartByteOffsets);
    }
}
