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

    private static AniEntry? Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return new AniDecoder().Read(stream);
    }

    /// <summary>
    /// RIFF pads a chunk of odd size with one byte that its size does not count, and Windows plays such a file.
    /// </summary>
    [Fact]
    public void Read_SkipsThePadByteAfterAnOddSizedChunk()
    {
        var entry = Decode(AniBuilder.FromFixture().WithChunkBeforeHeader(AniBuilder.Chunk("JUNK", [1, 2, 3])).Build());

        Assert.NotNull(entry);
        Assert.Equal(3u, entry.Header.NumFrames);
        Assert.Equal(3, entry.Frames.Count);
    }

    [Fact]
    public void Read_KeepsTheFirstValueOfAnInfoEntryThatRepeats()
    {
        var info = AniBuilder.InfoList(
            AniBuilder.Chunk("INAM", Encoding.ASCII.GetBytes("first\0")),
            AniBuilder.Chunk("INAM", Encoding.ASCII.GetBytes("second\0")));

        var entry = Decode(AniBuilder.FromFixture().WithChunkBeforeHeader(info).Build());

        Assert.NotNull(entry);
        Assert.Equal("first", entry.MetaData["INAM"]);
        Assert.Equal(3, entry.Frames.Count);
    }

    /// <summary>
    /// Some writers leave out the pad byte after odd sized INFO text. Windows ignores INFO entirely, so it must not cost
    /// the animation.
    /// </summary>
    [Fact]
    public void Read_ReadsPastInfoTextWrittenWithoutItsPadByte()
    {
        var info = AniBuilder.InfoList(
            AniBuilder.Chunk("INAM", Encoding.ASCII.GetBytes("abc"), pad: false),
            AniBuilder.Chunk("IART", Encoding.ASCII.GetBytes("de\0")));

        var entry = Decode(AniBuilder.FromFixture().WithChunkBeforeHeader(info).Build());

        Assert.NotNull(entry);
        Assert.Equal("abc", entry.MetaData["INAM"]);
        Assert.Equal("de", entry.MetaData["IART"]);
        Assert.Equal(3u, entry.Header.NumFrames);
        Assert.Equal(3, entry.Frames.Count);
    }

    [Fact]
    public void Read_StopsReadingInfoAtAnEntryThatRunsPastItsList()
    {
        var overrun = AniBuilder.Chunk("IART", Encoding.ASCII.GetBytes("de\0"));
        BitConverter.GetBytes(200u).CopyTo(overrun, 4);
        var info = AniBuilder.InfoList(AniBuilder.Chunk("INAM", Encoding.ASCII.GetBytes("abc\0")), overrun);

        var entry = Decode(AniBuilder.FromFixture().WithChunkBeforeHeader(info).Build());

        Assert.NotNull(entry);
        Assert.Equal("abc", Assert.Single(entry.MetaData).Value);
        Assert.Equal(3u, entry.Header.NumFrames);
        Assert.Equal(3, entry.Frames.Count);
    }

    [Fact]
    public void Read_CollectsOnlyIconSubChunksAsFrames()
    {
        var frames = AniBuilder.FixtureFrames;
        var bytes = AniBuilder.Riff(
            AniBuilder.Header(3, 3, 10, AniBuilder.IconFlag),
            AniBuilder.FrameList(AniBuilder.Icon(frames[0]), AniBuilder.Chunk("JUNK", [1, 2]), AniBuilder.Icon(frames[1]), AniBuilder.Icon(frames[2])));

        var entry = Decode(bytes);

        Assert.NotNull(entry);
        Assert.Equal(3, entry.Frames.Count);
        for (var i = 0; i < frames.Count; i++)
            Assert.Equal(frames[i], bytes.Skip((int)entry.Frames[i].Offset).Take(frames[i].Length));
    }

    /// <summary>
    /// Windows plays an animation whatever size its RIFF header states, walking the chunks to the end of the data.
    /// </summary>
    [Theory]
    [InlineData(0u)]
    [InlineData(4u)]
    [InlineData(4u + 8 + 36)]
    public void Read_WalksTheChunksToTheEndOfTheDataWhateverTheRiffSize(uint riffSize)
    {
        var bytes = AniBuilder.FromFixture().Build();
        BitConverter.GetBytes(riffSize).CopyTo(bytes, 4);

        var entry = Decode(bytes);

        Assert.NotNull(entry);
        Assert.Equal(3, entry.Frames.Count);
    }

    [Fact]
    public void Read_EndsAFrameCutShortAtTheEndOfTheData()
    {
        var bytes = AniBuilder.FromFixture().Build();
        Array.Resize(ref bytes, bytes.Length - 20);

        var entry = Decode(bytes);

        Assert.NotNull(entry);
        var lastFrame = entry.Frames.Last();
        Assert.Equal(bytes.Length, lastFrame.Offset + lastFrame.Size);
    }

    [Fact]
    public void Read_LeavesHeaderFieldsPastItsChunkZero()
    {
        var bytes = AniBuilder.Riff(
            AniBuilder.Header(3, 3, 10, AniBuilder.IconFlag, chunkSize: 32),
            AniBuilder.Values("seq ", 0, 1, 2),
            AniBuilder.FrameList([.. AniBuilder.FixtureFrames.Select(AniBuilder.Icon)]));

        var entry = Decode(bytes);

        Assert.NotNull(entry);
        Assert.Equal(10u, entry.Header.DisplayRate);
        Assert.False(entry.Header.Flags.IconFlag);
        Assert.Equal(0u, entry.Header.Flags.Reserved);
        Assert.Equal([0u, 1u, 2u], entry.FrameSequence);
        Assert.Equal(3, entry.Frames.Count);
    }

    [Fact]
    public void Read_ReturnsNullWithoutAHeaderChunk() => Assert.Null(Decode(AniBuilder.Riff()));

    [Fact]
    public void Read_ReturnsNullWithoutFrames()
        => Assert.Null(Decode(AniBuilder.Riff(AniBuilder.Header(3, 3, 10, AniBuilder.IconFlag), AniBuilder.FrameList())));

    /// <summary>
    /// Windows shows such a file as a still cursor rather than an animation.
    /// </summary>
    [Fact]
    public void Read_ReturnsNullWhenTheFrameListComesBeforeTheHeader()
        => Assert.Null(Decode(AniBuilder.FromFixture().WithFrameListBeforeHeader().Build()));

    [Fact]
    public void Read_TruncatedFile_ReturnsNull()
    {
        var bytes = TestFiles.BytesOf(TestFiles.AnimatedThreeFrame).Take(40).ToArray();
        using var stream = new MemoryStream(bytes);

        Assert.Null(new AniDecoder().Read(stream));
    }
}
