using System.Text;

using Ani.Reader.Decoder;
using Ani.Reader.Test.Fixtures;
using Ani.Reader.Test.Imaging;

using Ico.Reader;

using ImageMagick;

using Xunit;

namespace Ani.Reader.Test.Unit;

/// <summary>
/// Positive controls for <see cref="AniBuilder"/> and <see cref="CursorBuilder"/>, so a test built on them fails because of
/// the reader and not because of the bytes it was given.
/// </summary>
public sealed class AniBuilderTests
{
    private const int HeaderDataOffset = 20;

    [Fact]
    public void Build_FromTheFixtureFrames_ReproducesTheFixture()
        => Assert.Equal(TestFiles.BytesOf(TestFiles.AnimatedThreeFrame), AniBuilder.FromFixture().Build());

    [Fact]
    public void Build_RecordsWhereEachFrameStarts()
    {
        var builder = AniBuilder.FromFixture().WithRates(10, 20, 30).WithSequence(2, 0, 1);
        using var stream = new MemoryStream(builder.Build());

        var entry = new AniDecoder().Read(stream)
            ?? throw new InvalidOperationException("The built animation failed to decode.");

        Assert.Equal(builder.FrameOffsets, entry.Frames.Select(frame => frame.Offset));
        Assert.Equal([10u, 20u, 30u], entry.FrameRates);
        Assert.Equal([2u, 0u, 1u], entry.FrameSequence);
    }

    [Fact]
    public void Build_WritesTheCountsAndFlagsItIsGiven()
    {
        var bytes = AniBuilder.FromFixture().WithFrameCount(2).WithStepCount(5).WithFlags(0).Build();

        Assert.Equal("anih", Encoding.ASCII.GetString(bytes, HeaderDataOffset - 8, 4));
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, HeaderDataOffset + 4));
        Assert.Equal(5u, BitConverter.ToUInt32(bytes, HeaderDataOffset + 8));
        Assert.Equal(0u, BitConverter.ToUInt32(bytes, HeaderDataOffset + 32));
    }

    [Fact]
    public void Build_PadsTheChunksItIsGivenAndKeepsTheRiffSizeExact()
    {
        var bytes = AniBuilder.FromFixture()
            .WithChunkBeforeHeader(AniBuilder.Chunk("JUNK", [1, 2, 3]))
            .Build();

        Assert.Equal("JUNK", Encoding.ASCII.GetString(bytes, 12, 4));
        Assert.Equal(3u, BitConverter.ToUInt32(bytes, 16));
        Assert.Equal("anih", Encoding.ASCII.GetString(bytes, 24, 4));
        Assert.Equal((uint)(bytes.Length - 8), BitConverter.ToUInt32(bytes, 4));
    }

    [Fact]
    public void Build_CanWriteTheFrameListBeforeTheHeader()
    {
        var builder = AniBuilder.FromFixture().WithFrameListBeforeHeader();
        var bytes = builder.Build();

        Assert.Equal("LIST", Encoding.ASCII.GetString(bytes, 12, 4));
        Assert.Equal("anih", Encoding.ASCII.GetString(bytes, bytes.Length - 44, 4));
        Assert.Equal(AniBuilder.FixtureFrames[0], bytes.AsSpan((int)builder.FrameOffsets[0], AniBuilder.FixtureFrames[0].Length).ToArray());
    }

    [Fact]
    public void CursorBuilder_WritesOneSolidImagePerSize()
    {
        var icoData = new IcoReader().Read(CursorBuilder.Solid((255, 0, 0), 32, 48));

        Assert.NotNull(icoData);
        Assert.Equal([32, 48], icoData.ImageReferences.Select(reference => reference.Width));
        using var image = Pixels.Decode(icoData.GetImage(1));
        Assert.Equal(48 * 48, Pixels.CountMatching(image, new MagickColor("#FF0000")));
    }
}
