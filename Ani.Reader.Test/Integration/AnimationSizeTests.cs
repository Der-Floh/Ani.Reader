using Ani.Reader.Models;
using Ani.Reader.Test.Fixtures;
using Ani.Reader.Test.Imaging;

using Ico.Reader;

using Xunit;

namespace Ani.Reader.Test.Integration;

/// <summary>
/// <see cref="AniData.Animations"/> lists the sizes the animation can be played back at, and a frame asked for a size
/// it does not hold gives the image closest to it.
/// </summary>
public sealed class AnimationSizeTests
{
    [Fact]
    public void Animations_ListTheSizesEveryFrameHolds()
    {
        var aniData = Read(Frame(32, 48), Frame(48, 64), Frame(16, 48));

        Assert.Equal([48], aniData.Animations.Select(animation => animation.Width));
    }

    [Fact]
    public void Animations_IgnoreAFrameWithoutAnImage()
    {
        var aniData = Read(Frame(32, 48), AniBuilder.EmptyCursor, Frame(48, 64));

        Assert.Equal([48], aniData.Animations.Select(animation => animation.Width));
    }

    [Fact]
    public async Task Animations_ListTheSizesOfTheFirstFrameWhenTheFramesShareNone()
    {
        var aniData = Read(Frame(32), Frame(48), Frame(48));

        var animation = Assert.Single(aniData.Animations);
        Assert.Equal(32, animation.Width);
        using var image = Pixels.Decode((await aniData.GetFrameBytes(animation, aniData.Frames[1]))!);
        Assert.Equal(48u, image.Width);
    }

    [Fact]
    public void Animations_DoNotStartOverOnceTheFramesShareNoSize()
    {
        var aniData = Read(Frame(32, 48), Frame(64), Frame(48, 96));

        Assert.Equal([32, 48], aniData.Animations.Select(animation => animation.Width));
    }

    [Theory]
    [InlineData(new[] { 64, 16 }, 16)]
    [InlineData(new[] { 16, 48 }, 48)]
    public void FindByAnimationInformation_TakesTheClosestSizeThenTheLarger(int[] sizes, int expectedWidth)
    {
        var aniData = Read(Frame(32));
        var icoData = new IcoReader().Read(Frame(sizes))!;

        var image = aniData.FindByAnimationInformation(icoData, aniData.Animations[0]);

        Assert.Equal(expectedWidth, image.Width);
    }

    private static byte[] Frame(params int[] sizes) => CursorBuilder.Solid((255, 0, 0), sizes);

    private static AniData Read(params byte[][] frames)
    {
        var builder = new AniBuilder();
        foreach (var frame in frames)
            builder.AddFrame(frame);

        return Assert.Single(new AniReader().Read(builder.Build())!);
    }
}
