using System.Text;

using Ani.Reader.Models;
using Ani.Reader.Test.Fixtures;

using Xunit;

namespace Ani.Reader.Test.Integration;

/// <summary>
/// Animations checked against Windows with <c>LoadImage</c> and <c>GetCursorFrameInfo</c>. One that Windows loads plays
/// exactly the steps Windows plays, and one that Windows refuses is read as far as it can be.
/// </summary>
public sealed class WindowsPlaybackTests
{
    private const uint Icon = AniBuilder.IconFlag;
    private const uint IconAndSequence = AniBuilder.IconFlag | AniBuilder.SequenceFlag;

    private const int NotIconData = 3;
    private const int NoImages = 4;
    private const int SmallCursor = 5;
    private const int OddSized = 6;
    private const int Empty = 7;

    private static readonly byte[][] FramePool =
    [
        AniBuilder.FixtureFrames[0],
        AniBuilder.FixtureFrames[1],
        AniBuilder.FixtureFrames[2],
        [0xFF, .. new byte[63]],
        AniBuilder.EmptyCursor,
        CursorBuilder.Solid((255, 0, 255), 16),
        [.. AniBuilder.FixtureFrames[2], 0x55],
        [],
    ];

    public static TheoryData<string, byte[], int[], uint[]> AnimationsWindowsLoads => new()
    {
        { "rate and seq", Ani(Header(3, 3, flags: IconAndSequence), Rates(5, 10, 15), Sequence(2, 0, 1), FrameList(0, 1, 2)), [2, 0, 1], [5, 10, 15] },
        { "seq without the sequence flag", Ani(Header(3, 3), Sequence(2, 0, 1), FrameList(0, 1, 2)), [2, 0, 1], [10, 10, 10] },
        { "the sequence flag without seq", Ani(Header(3, 3, flags: IconAndSequence), FrameList(0, 1, 2)), [0, 1, 2], [10, 10, 10] },
        { "fewer steps than frames", Ani(Header(3, 2), FrameList(0, 1, 2)), [0, 1], [10, 10] },
        { "fewer declared frames than stored", Ani(Header(2, 2), FrameList(0, 1, 2)), [0, 1], [10, 10] },
        { "seq showing frames more than once", Ani(Header(3, 4, flags: IconAndSequence), Sequence(0, 1, 0, 1), FrameList(0, 1, 2)), [0, 1, 0, 1], [10, 10, 10, 10] },
        { "a rate of 0", Ani(Header(3, 3), Rates(5, 0, 7), FrameList(0, 1, 2)), [0, 1, 2], [5, 0, 7] },
        { "display rate 0 with a rate chunk", Ani(Header(3, 3, rate: 0), Rates(5, 6, 7), FrameList(0, 1, 2)), [0, 1, 2], [5, 6, 7] },
        { "rate after the frames", Ani(Header(3, 3), FrameList(0, 1, 2), Rates(5, 6, 7)), [0, 1, 2], [5, 6, 7] },
        { "seq after the frames", Ani(Header(3, 3, flags: IconAndSequence), FrameList(0, 1, 2), Sequence(2, 0, 1)), [2, 0, 1], [10, 10, 10] },
        { "the last of two rate chunks", Ani(Header(3, 3), Rates(5, 6, 7), Rates(8, 9, 10), FrameList(0, 1, 2)), [0, 1, 2], [8, 9, 10] },
        { "the last of two seq chunks", Ani(Header(3, 3, flags: IconAndSequence), Sequence(2, 0, 1), Sequence(1, 2, 0), FrameList(0, 1, 2)), [1, 2, 0], [10, 10, 10] },
        { "a second header discarding the rates", Ani(Header(3, 3), Rates(5, 6, 7), Header(3, 3, rate: 20), FrameList(0, 1, 2)), [0, 1, 2], [20, 20, 20] },
        { "a second header discarding the sequence", Ani(Header(3, 3, flags: IconAndSequence), Sequence(2, 0, 1), Header(3, 3, flags: IconAndSequence), FrameList(0, 1, 2)), [0, 1, 2], [10, 10, 10] },
        { "the last of two headers", Ani(Header(3, 3), Header(2, 2, rate: 20), FrameList(0, 1, 2)), [0, 1], [20, 20] },
        { "a header after a frame list without frames", Ani(Header(2, 2), FrameList(), Header(3, 3), FrameList(0, 1, 2)), [0, 1, 2], [10, 10, 10] },
        { "reserved flag bits", Ani(Header(3, 3, flags: Icon | 0x100), FrameList(0, 1, 2)), [0, 1, 2], [10, 10, 10] },
        { "frames split over two lists", Ani(Header(3, 3), FrameList(0, 1), Rates(5, 6, 7), FrameList(2)), [0, 1, 2], [5, 6, 7] },
        { "another chunk between frames", Ani(Header(3, 3), AniBuilder.FrameList(IconOf(0), AniBuilder.Chunk("JUNK", [1, 2]), IconOf(1), IconOf(2))), [0, 1, 2], [10, 10, 10] },
        { "an odd sized chunk before the header", Ani(AniBuilder.Chunk("JUNK", [1, 2, 3]), Header(3, 3), FrameList(0, 1, 2)), [0, 1, 2], [10, 10, 10] },
        { "a zero sized chunk", Ani(AniBuilder.Chunk("JUNK", []), Header(3, 3), FrameList(0, 1, 2)), [0, 1, 2], [10, 10, 10] },
        { "INFO text without its pad byte", Ani(AniBuilder.InfoList(Text("INAM", "abc", pad: false), Text("IART", "de\0")), Header(3, 3), FrameList(0, 1, 2)), [0, 1, 2], [10, 10, 10] },
        { "a repeated INFO entry", Ani(AniBuilder.InfoList(Text("INAM", "a\0"), Text("INAM", "b\0")), Header(3, 3), FrameList(0, 1, 2)), [0, 1, 2], [10, 10, 10] },
        { "a frame past the declared count that is not icon data", Ani(Header(2, 2), FrameList(0, 1, NotIconData)), [0, 1], [10, 10] },
        { "a frame past the declared count without images", Ani(Header(2, 2), FrameList(0, 1, NoImages)), [0, 1], [10, 10] },
        { "frames of different sizes", Ani(Header(3, 3), FrameList(0, SmallCursor, 2)), [0, SmallCursor, 2], [10, 10, 10] },
        { "an odd sized last frame without pad bytes at the end of the data", Ani(Header(3, 3), AniBuilder.Chunk("LIST", [.. Encoding.ASCII.GetBytes("fram"), .. IconOf(0), .. IconOf(1), .. AniBuilder.Chunk("icon", FramePool[OddSized], pad: false)], pad: false)), [0, 1, OddSized], [10, 10, 10] },
        { "a RIFF size of 4", WithRiffSize(Ani(Header(3, 3), FrameList(0, 1, 2)), 4), [0, 1, 2], [10, 10, 10] },
        { "stray bytes after the last chunk", [.. Ani(Header(3, 3), FrameList(0, 1, 2)), 1, 2, 3, 4, 5, 6, 7], [0, 1, 2], [10, 10, 10] },
        { "a second RIFF form after the first", [.. Ani(Header(3, 3), FrameList(0, 1, 2)), .. Ani(Header(2, 2, rate: 20), FrameList(0, 1))], [0, 1, 2], [10, 10, 10] },
    };

    public static TheoryData<string, byte[], int[], uint[]> AnimationsWindowsRefuses => new()
    {
        { "rate holding more entries than steps", Ani(Header(3, 3), Rates(5, 6, 7, 8), FrameList(0, 1, 2)), [0, 1, 2], [5, 6, 7] },
        { "rate holding fewer entries than steps", Ani(Header(3, 3), Rates(5), FrameList(0, 1, 2)), [0, 1, 2], [5, 10, 10] },
        { "seq holding more entries than steps", Ani(Header(3, 3, flags: IconAndSequence), Sequence(2, 0, 1, 0), FrameList(0, 1, 2)), [2, 0, 1, 0], [10, 10, 10, 10] },
        { "seq holding fewer entries than steps", Ani(Header(3, 4, flags: IconAndSequence), Sequence(2, 0, 1), FrameList(0, 1, 2)), [2, 0, 1], [10, 10, 10] },
        { "more steps than frames without seq", Ani(Header(3, 4), FrameList(0, 1, 2)), [0, 1, 2], [10, 10, 10] },
        { "no steps", Ani(Header(3, 0), FrameList(0, 1, 2)), [0, 1, 2], [10, 10, 10] },
        { "more steps than declared frames", Ani(Header(2, 3), FrameList(0, 1, 2)), [0, 1, 2], [10, 10, 10] },
        { "more declared frames than stored", Ani(Header(4, 3), FrameList(0, 1, 2)), [0, 1, 2], [10, 10, 10] },
        { "no declared frames", Ani(Header(0, 3), FrameList(0, 1, 2)), [0, 1, 2], [10, 10, 10] },
        { "seq naming a frame past the last", Ani(Header(3, 3, flags: IconAndSequence), Sequence(0, 5, 1), FrameList(0, 1, 2)), [0, 2, 1], [10, 10, 10] },
        { "seq naming a stored frame past the declared count", Ani(Header(2, 2, flags: IconAndSequence), Sequence(0, 2), FrameList(0, 1, 2)), [0, 2], [10, 10] },
        { "a header stating a size of 32", Ani(AniBuilder.Header(3, 3, 10, Icon, headerSize: 32), FrameList(0, 1, 2)), [0, 1, 2], [10, 10, 10] },
        { "a header stating a size of 0", Ani(AniBuilder.Header(3, 3, 10, Icon, headerSize: 0), FrameList(0, 1, 2)), [0, 1, 2], [10, 10, 10] },
        { "a header chunk of 40 bytes", Ani(AniBuilder.Header(3, 3, 10, Icon, chunkSize: 40), FrameList(0, 1, 2)), [0, 1, 2], [10, 10, 10] },
        { "display rate 0 without a rate chunk", Ani(Header(3, 3, rate: 0), FrameList(0, 1, 2)), [0, 1, 2], [0, 0, 0] },
        { "a rate chunk of 13 bytes", Ani(Header(3, 3), AniBuilder.Chunk("rate", [.. Rates(5, 6, 7).Skip(8), 9]), FrameList(0, 1, 2)), [0, 1, 2], [5, 6, 7] },
        { "a seq chunk of 13 bytes", Ani(Header(3, 3, flags: IconAndSequence), AniBuilder.Chunk("seq ", [.. Sequence(2, 0, 1).Skip(8), 9]), FrameList(0, 1, 2)), [2, 0, 1], [10, 10, 10] },
        { "a malformed rate chunk before a valid one", Ani(Header(3, 3), AniBuilder.Chunk("rate", [.. Rates(5, 6, 7).Skip(8), 9]), Rates(8, 9, 10), FrameList(0, 1, 2)), [0, 1, 2], [8, 9, 10] },
        { "rate before the header", Ani(Rates(5, 6, 7), Header(3, 3), FrameList(0, 1, 2)), [0, 1, 2], [5, 6, 7] },
        { "seq before the header", Ani(Sequence(2, 0, 1), Header(3, 3, flags: IconAndSequence), FrameList(0, 1, 2)), [2, 0, 1], [10, 10, 10] },
        { "a header after the frames", Ani(Header(3, 3), FrameList(0, 1, 2), Header(2, 2, rate: 20)), [0, 1, 2], [20, 20, 20] },
        { "a LIST chunk too small for its type", Ani(AniBuilder.Chunk("LIST", [1, 2]), Header(3, 3), FrameList(0, 1, 2)), [0, 1, 2], [10, 10, 10] },
        { "a chunk running past the end of the data", Ani(Header(3, 3), FrameList(0, 1, 2), WithChunkSize(AniBuilder.Chunk("JUNK", [1, 2]), 5000)), [0, 1, 2], [10, 10, 10] },
        { "an odd sized chunk without its pad byte at the end of the data", Ani(Header(3, 3), FrameList(0, 1, 2), AniBuilder.Chunk("JUNK", [1, 2, 3], pad: false)), [0, 1, 2], [10, 10, 10] },
        { "a frame list running past the end of the data", Ani(Header(3, 3), WithChunkSize(FrameList(0, 1, 2), (uint)(FrameList(0, 1, 2).Length - 8 + 1000))), [0, 1, 2], [10, 10, 10] },
        { "a frame list whose size leaves out its last frame", Ani(Header(3, 3), WithChunkSize(FrameList(0, 1, 2), (uint)(FrameList(0, 1).Length - 8))), [0, 1], [10, 10] },
        { "a frame that is not icon data", Ani(Header(3, 3), FrameList(0, NotIconData, 2)), [0, NotIconData, 2], [10, 10, 10] },
        { "a frame without images", Ani(Header(3, 3), FrameList(0, NoImages, 2)), [0, NoImages, 2], [10, 10, 10] },
        { "a first frame without images", Ani(Header(3, 3), FrameList(NoImages, 1, 2)), [NoImages, 1, 2], [10, 10, 10] },
        { "a frame of 0 bytes", Ani(Header(3, 3), FrameList(0, Empty, 2)), [0, Empty, 2], [10, 10, 10] },
        { "a declared frame no step shows that is not icon data", Ani(Header(3, 2, flags: IconAndSequence), Sequence(0, 2), FrameList(0, NotIconData, 2)), [0, 2], [10, 10] },
    };

    [Theory]
    [MemberData(nameof(AnimationsWindowsLoads))]
    public void Read_PlaysTheStepsWindowsPlays(string layout, byte[] bytes, int[] frames, uint[] jiffies)
    {
        var aniData = Assert.Single(new AniReader().Read(bytes)!);

        Assert.True(aniData.LoadsOnWindows, layout);
        AssertSteps(bytes, aniData, frames, jiffies);
    }

    [Theory]
    [MemberData(nameof(AnimationsWindowsRefuses))]
    public void Read_ReadsWhatItCanOfAnAnimationWindowsRefuses(string layout, byte[] bytes, int[] frames, uint[] jiffies)
    {
        var aniData = Assert.Single(new AniReader().Read(bytes)!);

        Assert.False(aniData.LoadsOnWindows, layout);
        AssertSteps(bytes, aniData, frames, jiffies);
    }

    [Fact]
    public async Task GetFrameBytes_ReturnsNullForAFrameCutShortByTheEndOfTheData()
    {
        var aniData = Assert.Single(new AniReader().Read(AnimationWithItsLastFrameCutShort())!);

        Assert.False(aniData.LoadsOnWindows);
        Assert.Equal(3, aniData.Frames.Count);
        Assert.NotNull(await aniData.GetFrameBytes(aniData.Animations[0], aniData.Frames[1]));
        Assert.Null(await aniData.GetFrameBytes(aniData.Animations[0], aniData.Frames[2]));
    }

    [Fact]
    public async Task SaveImages_SkipsAStepWhoseImageCannotBeDecoded()
    {
        var aniData = Assert.Single(new AniReader().Read(AnimationWithItsLastFrameCutShort())!);
        using var directory = new TemporaryDirectory();

        await aniData.SaveImages(directory.Path, aniData.Animations[0]);

        var saved = Directory.GetFiles(directory.Path, "*.png", SearchOption.AllDirectories).Select(Path.GetFileName);
        Assert.Equal(["frame_0 (32x32 32 bit).png", "frame_1 (32x32 32 bit).png"], saved.OrderBy(name => name, StringComparer.Ordinal));
    }

    private static byte[] AnimationWithItsLastFrameCutShort()
    {
        var bytes = Ani(
            Header(3, 3),
            AniBuilder.FrameList(
                AniBuilder.Icon(CursorBuilder.Solid((255, 0, 0), 32)),
                AniBuilder.Icon(CursorBuilder.Solid((0, 255, 0), 32)),
                AniBuilder.Icon(CursorBuilder.Solid((0, 0, 255), 32))));
        Array.Resize(ref bytes, bytes.Length - 20);

        return bytes;
    }

    [Theory]
    [InlineData(NotIconData)]
    [InlineData(NoImages)]
    [InlineData(Empty)]
    public async Task GetFrameBytes_ReturnsNullForAStepWhoseFrameHoldsNoImage(int frame)
    {
        var aniData = Assert.Single(new AniReader().Read(Ani(Header(3, 3), FrameList(0, frame, 2)))!);

        Assert.Empty(aniData.Frames[1].VariationDetails);
        Assert.Null(await aniData.GetFrameBytes(aniData.Animations[0], aniData.Frames[1]));
        Assert.NotNull(await aniData.GetFrameBytes(aniData.Animations[0], aniData.Frames[2]));
    }

    [Fact]
    public void Read_ReturnsNullWhenNoFrameHoldsAnImage()
        => Assert.Null(new AniReader().Read(Ani(Header(3, 3), FrameList(NoImages, NotIconData, Empty))));

    [Fact]
    public void Read_ReturnsNullWhenTheIconFlagIsClear()
        => Assert.Null(new AniReader().Read(Ani(Header(3, 3, flags: 0), FrameList(0, 1, 2))));

    [Fact]
    public void Read_ReturnsNullWhenTheHeaderEndsBeforeItsFlags()
        => Assert.Null(new AniReader().Read(Ani(AniBuilder.Header(3, 3, 10, Icon, chunkSize: 32, headerSize: 32), FrameList(0, 1, 2))));

    private static void AssertSteps(byte[] bytes, AniData aniData, int[] frames, uint[] jiffies)
    {
        Assert.Equal(frames.Length, aniData.Frames.Count);
        for (var step = 0; step < frames.Length; step++)
        {
            var expected = FramePool[frames[step]];
            var shown = bytes.Skip((int)aniData.Frames[step].FrameReference.Offset).Take(expected.Length);
            Assert.True(shown.SequenceEqual(expected), $"Step {step} does not show frame {frames[step]}.");
        }

        Assert.Equal(jiffies.Select(rate => TimeSpan.FromSeconds(rate / 60.0)), aniData.Frames.Select(frame => frame.Duration));
    }

    private static byte[] Ani(params byte[][] chunks) => AniBuilder.Riff(chunks);

    private static byte[] Header(uint frameCount, uint stepCount, uint rate = 10, uint flags = Icon)
        => AniBuilder.Header(frameCount, stepCount, rate, flags);

    private static byte[] FrameList(params int[] frames) => AniBuilder.FrameList([.. frames.Select(IconOf)]);

    private static byte[] IconOf(int frame) => AniBuilder.Icon(FramePool[frame]);

    private static byte[] Rates(params uint[] rates) => AniBuilder.Values("rate", rates);

    private static byte[] Sequence(params uint[] sequence) => AniBuilder.Values("seq ", sequence);

    private static byte[] Text(string id, string text, bool pad = true) => AniBuilder.Chunk(id, Encoding.ASCII.GetBytes(text), pad);

    private static byte[] WithChunkSize(byte[] chunk, uint size)
    {
        var copy = chunk.ToArray();
        BitConverter.GetBytes(size).CopyTo(copy, 4);
        return copy;
    }

    private static byte[] WithRiffSize(byte[] ani, uint size) => WithChunkSize(ani, size);
}
