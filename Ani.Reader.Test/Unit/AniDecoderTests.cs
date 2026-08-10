using System.Text;

using Ani.Reader.Decoder;
using Ani.Reader.Models;
using Ani.Reader.Test.Fixtures;

using Xunit;

namespace Ani.Reader.Test.Unit;

public sealed class AniDecoderTests
{
    private static AniEntry DecodeFixture()
    {
        using var stream = File.OpenRead(TestFiles.PathTo(TestFiles.AnimatedThreeFrame));
        return new AniDecoder().Read(stream)
            ?? throw new InvalidOperationException("The fixture failed to decode.");
    }

    [Fact]
    public void Read_ParsesTheAnihChunk()
    {
        var entry = DecodeFixture();

        Assert.Equal(36u, entry.Header.HeaderSize);
        Assert.Equal(3u, entry.Header.NumFrames);
        Assert.Equal(3u, entry.Header.NumSteps);
        Assert.Equal(10u, entry.Header.DisplayRate);
        Assert.True(entry.Header.Flags.IconFlag);
        Assert.False(entry.Header.Flags.SequenceFlag);
    }

    [Fact]
    public void Read_LeavesDimensionFieldsZero_WhenFramesCarryTheirOwnSize()
    {
        var entry = DecodeFixture();

        Assert.Equal(0u, entry.Header.Width);
        Assert.Equal(0u, entry.Header.Height);
        Assert.Equal(0u, entry.Header.BitCount);
    }

    [Fact]
    public void Read_CollectsOneReferencePerFrame()
    {
        var entry = DecodeFixture();

        Assert.Equal(3, entry.Frames.Count);
        Assert.All(entry.Frames, frame => Assert.True(frame.Size > 0));
        Assert.All(entry.Frames, frame => Assert.Equal(frame.Offset, frame.RealOffset));

        var offsets = entry.Frames.Select(f => f.Offset).ToArray();
        Assert.Equal(offsets.OrderBy(o => o), offsets);
    }

    [Fact]
    public void Read_WithoutRateOrSeqChunks_LeavesBothListsEmpty()
    {
        var entry = DecodeFixture();

        Assert.Empty(entry.FrameRates);
        Assert.Empty(entry.FrameSequence);
    }

    [Fact]
    public void Read_WithOffsetAndSize_RebasesFrameOffsets()
    {
        var padding = new byte[16];
        var aniBytes = TestFiles.BytesOf(TestFiles.AnimatedThreeFrame);
        using var stream = new MemoryStream([.. padding, .. aniBytes]);

        var entry = new AniDecoder().Read(stream, padding.Length, aniBytes.Length)
            ?? throw new InvalidOperationException("The padded fixture failed to decode.");

        Assert.Equal((long)padding.Length, entry.EntryOffset);
        Assert.Equal((long)aniBytes.Length, entry.EntrySize);
        Assert.All(entry.Frames, frame => Assert.Equal(frame.Offset + padding.Length, frame.RealOffset));
    }

    [Theory]
    [InlineData(TestFiles.StaticBig)]
    [InlineData(TestFiles.StaticMultiSize)]
    public void Read_NonRiffData_ReturnsNull(string fileName)
    {
        using var stream = File.OpenRead(TestFiles.PathTo(fileName));

        Assert.Null(new AniDecoder().Read(stream));
    }

    [Fact]
    public void Read_EmptyStream_ReturnsNull()
    {
        using var stream = new MemoryStream();

        Assert.Null(new AniDecoder().Read(stream));
    }

    [Fact]
    public void Read_RiffWithoutAconMarker_ReturnsNull()
    {
        var bytes = new List<byte>();
        bytes.AddRange(Encoding.ASCII.GetBytes("RIFF"));
        bytes.AddRange(BitConverter.GetBytes(16u));
        bytes.AddRange(Encoding.ASCII.GetBytes("WAVE"));
        using var stream = new MemoryStream([.. bytes]);

        Assert.Null(new AniDecoder().Read(stream));
    }

    [Fact]
    public void Read_TruncatedFile_ReturnsNull()
    {
        var bytes = TestFiles.BytesOf(TestFiles.AnimatedThreeFrame)[..40];
        using var stream = new MemoryStream(bytes);

        Assert.Null(new AniDecoder().Read(stream));
    }
}
