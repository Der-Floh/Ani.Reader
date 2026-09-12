using Ani.Reader.Models;
using Ani.Reader.Test.Fixtures;
using Ani.Reader.Test.Imaging;

using Ico.Reader;

using Xunit;

namespace Ani.Reader.Test.Integration;

/// <summary>
/// Compares extracted images against images produced by a known-good build. Comparison is by decoded pixels
/// under a tolerance, never by encoded bytes, so a new PNG or WebP encoder in a dependency does not fail here.
/// </summary>
public sealed class GoldenImageTests
{
    private static readonly CursorFixture AnimatedFixture = CursorFixtures.For(TestFiles.AnimatedThreeFrame);

    private static AniData LoadAnimated() =>
        new AniReader().Read(AnimatedFixture.Path)?[0]
            ?? throw new InvalidOperationException($"{AnimatedFixture.FileName} failed to read.");

    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(64)]
    public async Task AniFrames_MatchTheGoldenImages(int width)
    {
        var aniData = LoadAnimated();
        var size = AnimatedFixture.SizeOf(width);

        foreach (var frame in aniData.Frames)
        {
            var png = await FrameImages.ExtractAsync(aniData, frame, width);

            Golden.VerifyImage(
                Path.Combine("ani", $"{size.Width}x{size.Height}", $"frame-{frame.Position}.png"),
                png,
                Golden.LosslessTolerance);
        }
    }

    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(64)]
    public async Task AniWebPExport_MatchesTheGoldenAnimation(int width)
    {
        var aniData = LoadAnimated();
        var animation = aniData.Animations.Single(a => a.Width == width);

        var webp = await aniData.GetWebpBytes(animation);

        Assert.NotNull(webp);
        Golden.VerifyAnimation(
            Path.Combine("ani", $"{animation.Width}x{animation.Height}.webp"),
            webp,
            AnimatedFixture.FrameCount,
            Golden.LosslessTolerance);
    }

    [Theory]
    [InlineData(TestFiles.StaticBig)]
    [InlineData(TestFiles.StaticBigTransparent)]
    [InlineData(TestFiles.StaticMultiSize)]
    [InlineData(TestFiles.StaticMultiSizeTransparent)]
    public async Task CursorImages_MatchTheGoldenImages(string fileName)
    {
        var fixture = CursorFixtures.For(fileName);
        var icoData = new IcoReader().Read(TestFiles.PathTo(fileName))
            ?? throw new InvalidOperationException($"{fileName} failed to read as cursor data.");

        foreach (var size in fixture.Sizes)
        {
            var reference = icoData.ImageReferences.Single(r => r.Width == size.Width);
            var png = await icoData.GetImageAsync(reference);

            Golden.VerifyImage(
                Path.Combine("cur", Path.GetFileNameWithoutExtension(fileName), $"{size.Width}x{size.Height}.png"),
                png,
                Golden.LosslessTolerance);
        }
    }
}
