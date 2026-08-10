using Ani.Reader.Models;
using Ani.Reader.Test.Fixtures;
using Ani.Reader.Test.Imaging;

using Xunit;

namespace Ani.Reader.Test.Integration;

public sealed class AniFrameImageTests
{
    private static readonly CursorFixture Fixture = CursorFixtures.For(TestFiles.AnimatedThreeFrame);

    private static AniData Load() =>
        new AniReader().Read(Fixture.Path)?[0]
            ?? throw new InvalidOperationException($"{Fixture.FileName} failed to read.");

    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(64)]
    public async Task Frames_DecodeToTheDeclaredSize(int width)
    {
        var aniData = Load();
        var size = Fixture.SizeOf(width);

        foreach (var frame in aniData.Frames)
        {
            using var image = Pixels.Decode(await FrameImages.ExtractAsync(aniData, frame, width));

            Assert.Equal(size.Width, (int)image.Width);
            Assert.Equal(size.Height, (int)image.Height);
        }
    }

    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(64)]
    public async Task Frames_CarryTheExpectedTransparency(int width)
    {
        var aniData = Load();
        var size = Fixture.SizeOf(width);

        foreach (var frame in aniData.Frames)
        {
            using var image = Pixels.Decode(await FrameImages.ExtractAsync(aniData, frame, width));

            Assert.Equal(size.TransparentPixelsPerFrame[frame.Position], Pixels.CountTransparent(image));
        }
    }

    [Fact]
    public async Task OnlyTheFramesTheFixtureNames_ContainTransparency()
    {
        var aniData = Load();

        foreach (var size in Fixture.Sizes)
        {
            foreach (var frame in aniData.Frames)
            {
                using var image = Pixels.Decode(await FrameImages.ExtractAsync(aniData, frame, size.Width));

                var isTransparentFrame = Fixture.TransparentFrames.Contains(frame.Position);
                Assert.Equal(isTransparentFrame, Pixels.CountTransparent(image) > 0);
            }
        }
    }

    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(64)]
    public async Task Frames_UseOnlyTheTwoFixtureColours(int width)
    {
        var aniData = Load();
        var size = Fixture.SizeOf(width);

        foreach (var frame in aniData.Frames)
        {
            using var image = Pixels.Decode(await FrameImages.ExtractAsync(aniData, frame, width));

            var foreground = Pixels.CountMatching(image, CursorFixtures.Foreground);
            var background = Pixels.CountMatching(image, CursorFixtures.Background);

            Assert.True(foreground > 0, $"{width}x frame {frame.Position} has no foreground pixels.");
            Assert.True(background > 0, $"{width}x frame {frame.Position} has no background pixels.");

            if (size.TransparentPixelsPerFrame[frame.Position] == 0)
                Assert.Equal(size.Width * size.Height, foreground + background);
        }
    }

    [Fact]
    public async Task GetFrameBytes_AgreesWithTheLowLevelExtraction()
    {
        var aniData = Load();

        foreach (var animation in aniData.Animations)
        {
            foreach (var frame in aniData.Frames)
            {
                var viaAnimation = await aniData.GetFrameBytes(animation, frame);
                var viaReference = await FrameImages.ExtractAsync(aniData, frame, animation.Width);

                Assert.NotNull(viaAnimation);
                Assert.Equal(viaReference, viaAnimation);
            }
        }
    }

    [Fact]
    public async Task SaveImages_WritesOnePngPerFrame()
    {
        var aniData = Load();
        var animation = aniData.Animations[0];
        using var directory = new TemporaryDirectory();

        await aniData.SaveImages(directory.Path, animation);

        var written = Directory.GetFiles(directory.Path, "*.png", SearchOption.AllDirectories);
        Assert.Equal(Fixture.FrameCount, written.Length);
        Assert.All(written, file => Assert.Contains($"{animation.Width}x{animation.Height}", file, StringComparison.Ordinal));
    }
}
