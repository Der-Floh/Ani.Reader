using Ani.Reader.Models;
using Ani.Reader.Test.Fixtures;

using Xunit;

namespace Ani.Reader.Test.Unit;

public sealed class AniReaderTests
{
    private static readonly AniReader Reader = new();

    [Fact]
    public void Read_Path_ReturnsOneEntryNamedAfterTheFile()
    {
        var result = Reader.Read(TestFiles.PathTo(TestFiles.AnimatedThreeFrame));

        var aniData = Assert.Single(result!);
        Assert.Equal("test-anim-t", aniData.Name);
        Assert.Equal(AniOriginFileType.Ani, aniData.Origin);
    }

    [Fact]
    public void Read_Bytes_ReturnsAnUnnamedEntry()
    {
        var result = Reader.Read(TestFiles.BytesOf(TestFiles.AnimatedThreeFrame));

        var aniData = Assert.Single(result!);
        Assert.Equal(string.Empty, aniData.Name);
        Assert.Equal(3, aniData.TotalFrames);
    }

    [Fact]
    public void Read_CopiedStream_SurvivesTheSourceBeingClosed()
    {
        AniData aniData;
        using (var stream = File.OpenRead(TestFiles.PathTo(TestFiles.AnimatedThreeFrame)))
        {
            aniData = Assert.Single(Reader.Read(stream, copyStream: true)!);
        }

        Assert.Equal(3, aniData.TotalFrames);
        Assert.Equal(3, aniData.Frames[0].VariationDetails.Count());
    }

    [Fact]
    public void Read_UncopiedStream_ReadsWhileTheSourceStaysOpen()
    {
        using var stream = File.OpenRead(TestFiles.PathTo(TestFiles.AnimatedThreeFrame));

        var aniData = Assert.Single(Reader.Read(stream, copyStream: false)!);

        Assert.Equal(3, aniData.TotalFrames);
        Assert.Equal(3, aniData.Frames[0].VariationDetails.Count());
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task Read_UncopiedStream_ExtractsFrameImagesFromTheCallerStream()
    {
        using var stream = File.OpenRead(TestFiles.PathTo(TestFiles.AnimatedThreeFrame));
        var aniData = Assert.Single(Reader.Read(stream, copyStream: false)!);

        var frameBytes = await aniData.GetFrameBytes(aniData.Animations[0], aniData.Frames[1]);

        Assert.NotNull(frameBytes);
        Assert.Equal(
            await new AniReader().Read(TestFiles.PathTo(TestFiles.AnimatedThreeFrame))![0]
                .GetFrameBytes(aniData.Animations[0], aniData.Frames[1]),
            frameBytes);
    }

    [Fact]
    public void Read_UncopiedNonSeekableStream_Throws()
    {
        using var stream = new NonSeekableStream(TestFiles.BytesOf(TestFiles.AnimatedThreeFrame));

        Assert.Throws<ArgumentException>(() => Reader.Read(stream, copyStream: false));
    }

    [Fact]
    public async Task Read_CopiedNonSeekableStream_ReadsTheAnimation()
    {
        using var stream = new NonSeekableStream(TestFiles.BytesOf(TestFiles.AnimatedThreeFrame));

        var aniData = Assert.Single(Reader.Read(stream, copyStream: true)!);

        Assert.Equal(3, aniData.TotalFrames);
        Assert.NotNull(await aniData.GetFrameBytes(aniData.Animations[0], aniData.Frames[2]));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Read_Stream_StartsAtItsCurrentPosition(bool copyStream)
    {
        var aniBytes = TestFiles.BytesOf(TestFiles.AnimatedThreeFrame);
        using var stream = new MemoryStream([.. new byte[7], .. aniBytes]) { Position = 7 };

        var aniData = Assert.Single(Reader.Read(stream, copyStream)!);

        var expected = Assert.Single(Reader.Read(aniBytes)!);
        Assert.Equal(expected.Frames.Select(frame => frame.FrameReference.RealOffset), aniData.Frames.Select(frame => frame.FrameReference.RealOffset));
        Assert.Equal(
            await expected.GetFrameBytes(expected.Animations[0], expected.Frames[1]),
            await aniData.GetFrameBytes(aniData.Animations[0], aniData.Frames[1]));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Read_NullStream_Throws(bool copyStream) => Assert.Throws<ArgumentNullException>(() => Reader.Read((Stream)null!, copyStream));

    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }

    [Fact]
    public void Read_EveryOverload_ProducesTheSameTiming()
    {
        var fromPath = Assert.Single(Reader.Read(TestFiles.PathTo(TestFiles.AnimatedThreeFrame))!);
        var fromBytes = Assert.Single(Reader.Read(TestFiles.BytesOf(TestFiles.AnimatedThreeFrame))!);

        Assert.Equal(fromPath.TotalFrames, fromBytes.TotalFrames);
        Assert.Equal(fromPath.TotalAnimationDuration, fromBytes.TotalAnimationDuration);
        Assert.Equal(fromPath.FrameRate, fromBytes.FrameRate);
        Assert.Equal(
            fromPath.Frames.Select(f => f.FrameReference.RealOffset),
            fromBytes.Frames.Select(f => f.FrameReference.RealOffset));
    }

    [Fact]
    public void Read_MissingFile_ReturnsNull()
    {
        Assert.Null(Reader.Read(TestFiles.PathTo("does-not-exist.ani")));
    }

    [Fact]
    public void Read_UnrecognisedBytes_ReturnsNull()
    {
        Assert.Null(Reader.Read(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
    }

    [Fact]
    public void Read_AnimationWithoutAHeader_ReturnsNull() => Assert.Null(Reader.Read(AniBuilder.Riff()));

    /// <summary>
    /// A .cur file is not RIFF, so it is not an ANI source. The cursor images inside an .ani frame are read
    /// through Ico.Reader instead; see <see cref="Integration.CursorFileTests"/>.
    /// </summary>
    [Theory]
    [InlineData(TestFiles.StaticBig)]
    [InlineData(TestFiles.StaticBigTransparent)]
    [InlineData(TestFiles.StaticMultiSize)]
    [InlineData(TestFiles.StaticMultiSizeTransparent)]
    public void Read_CursorFile_ReturnsNull(string fileName)
    {
        Assert.Null(Reader.Read(TestFiles.PathTo(fileName)));
        Assert.Null(Reader.Read(TestFiles.BytesOf(fileName)));
    }

    [Fact]
    public void Read_WithCustomConfiguration_UsesTheSuppliedDecoder()
    {
        var configuration = new AniReaderConfiguration { AniDecoder = new NullDecoder() };

        Assert.Null(new AniReader(configuration).Read(TestFiles.PathTo(TestFiles.AnimatedThreeFrame)));
    }

    private sealed class NullDecoder : Decoder.IAniDecoder
    {
        public AniEntry? Read(Stream stream) => null;

        public AniEntry? Read(Stream stream, long offset, long size) => null;
    }
}
