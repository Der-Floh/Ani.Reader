using Ani.Reader.Models;

using Xunit;

namespace Ani.Reader.Test.Unit;

public sealed class AniHeaderFlagsTests
{
    [Theory]
    [InlineData(0u, false, false, 0u)]
    [InlineData(1u, true, false, 0u)]
    [InlineData(2u, false, true, 0u)]
    [InlineData(3u, true, true, 0u)]
    [InlineData(4u, false, false, 1u)]
    [InlineData(0xFFu, true, true, 63u)]
    public void FromUInt32_SplitsFlagBits(uint value, bool iconFlag, bool sequenceFlag, uint reserved)
    {
        var flags = AniHeaderFlags.FromUInt32(value);

        Assert.Equal(iconFlag, flags.IconFlag);
        Assert.Equal(sequenceFlag, flags.SequenceFlag);
        Assert.Equal(reserved, flags.Reserved);
    }

    [Fact]
    public void ToString_ReportsEveryFlag()
    {
        var text = AniHeaderFlags.FromUInt32(3).ToString();

        Assert.Contains("IconFlag: True", text, StringComparison.Ordinal);
        Assert.Contains("SequenceFlag: True", text, StringComparison.Ordinal);
        Assert.Contains("Reserved: 0", text, StringComparison.Ordinal);
    }
}
