using ContractAI.Core.Interfaces;

namespace ContractAI.Tests.Domain;

// PageNumberForByteOffset is the only thing that turns the native parser's flat
// byte offsets back into a page number, so its boundary behaviour alone decides
// which page a clause is reported on.
public class ExtractedTextTests
{
    private static ExtractedText WithPageStarts(params int[] pageStartByteOffsets) =>
        new(Text: string.Empty, PageStartByteOffsets: pageStartByteOffsets);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5_000)]
    public void PageNumberForByteOffset_SinglePage_AlwaysReturnsFirstPage(int byteOffset)
    {
        var extracted = WithPageStarts(0);

        Assert.Equal(1, extracted.PageNumberForByteOffset(byteOffset));
    }

    // Pages start at 0/100/250, so byte 99 is still page 1 and byte 100 is the first
    // byte of page 2.
    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 2)]
    [InlineData(101, 2)]
    [InlineData(249, 2)]
    [InlineData(250, 3)]
    public void PageNumberForByteOffset_ResolvesOffsetToOwningPage(int byteOffset, int expectedPage)
    {
        var extracted = WithPageStarts(0, 100, 250);

        Assert.Equal(expectedPage, extracted.PageNumberForByteOffset(byteOffset));
    }

    [Fact]
    public void PageNumberForByteOffset_OffsetPastLastPageStart_ReturnsLastPage()
    {
        var extracted = WithPageStarts(0, 100, 250);

        Assert.Equal(3, extracted.PageNumberForByteOffset(10_000));
    }

    // Walks every boundary across enough pages to exercise the binary search rather
    // than a lucky midpoint: an off-by-one would report clauses on the page before
    // the one they sit on.
    [Fact]
    public void PageNumberForByteOffset_EveryPageStart_MapsToItsOwnPage()
    {
        var pageStarts = Enumerable.Range(0, 50).Select(page => page * 1_000).ToArray();
        var extracted = WithPageStarts(pageStarts);

        for (var index = 0; index < pageStarts.Length; index++)
        {
            Assert.Equal(index + 1, extracted.PageNumberForByteOffset(pageStarts[index]));
            Assert.Equal(index + 1, extracted.PageNumberForByteOffset(pageStarts[index] + 999));
        }
    }

    // A PDF that yielded no page boundaries still has to produce a usable page
    // number: 0 is not a page the viewer can render.
    [Fact]
    public void PageNumberForByteOffset_NoPageStarts_ReturnsFirstPage()
    {
        var extracted = WithPageStarts();

        Assert.Equal(1, extracted.PageNumberForByteOffset(42));
    }

    // Defensive: nothing can precede page 1, so a negative offset clamps forward
    // instead of returning page 0.
    [Fact]
    public void PageNumberForByteOffset_NegativeOffset_ReturnsFirstPage()
    {
        var extracted = WithPageStarts(0, 100);

        Assert.Equal(1, extracted.PageNumberForByteOffset(-1));
    }
}
