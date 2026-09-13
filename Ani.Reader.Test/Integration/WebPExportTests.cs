using Ani.Reader.Models;
using Ani.Reader.Test.Fixtures;
using Ani.Reader.Test.Imaging;

using ImageMagick;

using Xunit;

namespace Ani.Reader.Test.Integration;

public sealed class WebPExportTests
{
    private static readonly CursorFixture Fixture = CursorFixtures.For(TestFiles.AnimatedThreeFrame);

    private static AniData Load() =>
        new AniReader().Read(Fixture.Path)?[0]
            ?? throw new InvalidOperationException($"{Fixture.FileName} failed to read.");

    [Fact]
    public async Task GetWebpBytes_ProducesAWebPAnimationWithOneFrameEachStep()
    {
        var aniData = Load();
        var animation = aniData.Animations[0];

        var bytes = await aniData.GetWebpBytes(animation);

        Assert.NotNull(bytes);
        using var collection = new MagickImageCollection(bytes);
        Assert.Equal(Fixture.FrameCount, collection.Count);
        Assert.All(collection, frame => Assert.Equal(MagickFormat.WebP, frame.Format));
    }

    [Fact]
    public async Task GetWebpBytes_KeepsTheVariantDimensions()
    {
        var aniData = Load();

        foreach (var animation in aniData.Animations)
        {
            using var collection = new MagickImageCollection((await aniData.GetWebpBytes(animation))!);
            collection.Coalesce();

            Assert.All(collection, frame =>
            {
                Assert.Equal((uint)animation.Width, frame.Width);
                Assert.Equal((uint)animation.Height, frame.Height);
            });
        }
    }

    /// <summary>
    /// WebP stores delays in hundredths of a second, so a 166.66 ms frame cannot be represented exactly.
    /// Rounding puts it at 170 ms rather than the 160 ms truncation would give.
    /// </summary>
    [Fact]
    public async Task GetWebpBytes_RoundsTheFrameDelayToTheNearestHundredth()
    {
        var aniData = Load();
        var expectedDelay = (uint)Math.Round(Fixture.FrameDuration.TotalMilliseconds / 10, MidpointRounding.AwayFromZero);

        using var collection = new MagickImageCollection((await aniData.GetWebpBytes(aniData.Animations[0]))!);

        Assert.Equal(17u, expectedDelay);
        Assert.All(collection, frame => Assert.Equal(expectedDelay, frame.AnimationDelay));
    }

    [Fact]
    public async Task GetWebpBytes_KeepsTotalDurationWithinOneHundredthPerFrame()
    {
        var aniData = Load();

        var frames = WebPAnimation.ReadFrameInfo((await aniData.GetWebpBytes(aniData.Animations[0]))!);

        var exported = TimeSpan.FromMilliseconds(frames.Sum(f => f.DurationMilliseconds));
        var drift = (exported - aniData.TotalAnimationDuration).Duration();
        Assert.True(drift <= TimeSpan.FromMilliseconds(10 * Fixture.FrameCount), $"drift was {drift.TotalMilliseconds} ms");
    }

    [Fact]
    public async Task GetWebpBytes_PreservesTransparency()
    {
        var aniData = Load();
        var animation = aniData.Animations[0];
        var size = Fixture.SizeOf(animation.Width);

        var frames = WebPAnimation.Composite((await aniData.GetWebpBytes(animation))!);

        try
        {
            Assert.Equal(
                size.TransparentPixelsPerFrame,
                frames.Select(Pixels.CountTransparent));
        }
        finally
        {
            frames.ForEach(frame => frame.Dispose());
        }
    }

    [Fact]
    public async Task GetWebpBytes_IsLossless()
    {
        var aniData = Load();
        var animation = aniData.Animations[0];

        var frames = WebPAnimation.Composite((await aniData.GetWebpBytes(animation))!);

        try
        {
            foreach (var frame in aniData.Frames)
            {
                using var source = Pixels.Decode((await aniData.GetFrameBytes(animation, frame))!);

                Assert.Equal(0.0, Pixels.RootMeanSquaredError(frames[frame.Position], source));
                Assert.Equal(Pixels.DistinctColors(source), Pixels.DistinctColors(frames[frame.Position]));
            }
        }
        finally
        {
            frames.ForEach(frame => frame.Dispose());
        }
    }

    [Fact]
    public async Task GetWebpBytes_WritesLosslessSubChunks()
    {
        var aniData = Load();

        var webp = (await aniData.GetWebpBytes(aniData.Animations[0]))!;

        Assert.Contains("VP8L", System.Text.Encoding.ASCII.GetString(webp), StringComparison.Ordinal);
        Assert.DoesNotContain("VP8 ", System.Text.Encoding.ASCII.GetString(webp), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsWebP_ToADirectory_WritesANamedFile()
    {
        var aniData = Load();
        var animation = aniData.Animations[0];
        using var directory = new TemporaryDirectory();

        await aniData.SaveAsWebP(directory.Path, animation);

        var written = Assert.Single(Directory.GetFiles(directory.Path, "*.webp", SearchOption.AllDirectories));
        Assert.Contains($"{animation.Width}x{animation.Height}", written, StringComparison.Ordinal);
        Assert.True(new FileInfo(written).Length > 0);
    }

    [Fact]
    public async Task SaveAsWebP_ToAnExistingFilePath_OverwritesIt()
    {
        var aniData = Load();
        using var directory = new TemporaryDirectory();
        var target = directory.Combine("cursor.webp");
        await File.WriteAllBytesAsync(target, [0], TestContext.Current.CancellationToken);

        await aniData.SaveAsWebP(target, aniData.Animations[0]);

        Assert.True(new FileInfo(target).Length > 1);
    }

    [Fact]
    public async Task SaveAsWebP_ToANewPath_CreatesTheDirectoryAndWritesTheFile()
    {
        var aniData = Load();
        using var directory = new TemporaryDirectory();
        var target = directory.Combine("nested", "cursor.webp");

        await aniData.SaveAsWebP(target, aniData.Animations[0]);

        Assert.True(File.Exists(target));
        Assert.True(new FileInfo(target).Length > 0);
    }

    [Fact]
    public async Task SaveAsWebP_AndGetWebpBytes_ProduceTheSameFile()
    {
        var aniData = Load();
        var animation = aniData.Animations[0];
        using var directory = new TemporaryDirectory();
        var target = directory.Combine("cursor.webp");

        await aniData.SaveAsWebP(target, animation);

        Assert.Equal(
            await aniData.GetWebpBytes(animation),
            await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetWebpBytes_DropsAStepOfAnHourOrMoreWithoutShorteningTheOthers()
    {
        var aniData = Read(AniBuilder.FromFixture().WithRates(10, 216000, 30));

        Assert.Equal(new[] { 170, 500 }, await ExportedDurations(aniData));
    }

    [Fact]
    public async Task GetWebpBytes_EncodesTheFirstFrameAsAStillImageWhenEveryStepLastsAnHourOrMore()
    {
        var aniData = Read(AniBuilder.FromFixture().WithRates(216000, 216000, 216000));
        var animation = aniData.Animations[0];

        var bytes = await aniData.GetWebpBytes(animation);

        using var collection = new MagickImageCollection(bytes);
        var still = Assert.Single(collection);
        using var firstFrame = Pixels.Decode((await aniData.GetFrameBytes(animation, aniData.Frames[0]))!);
        Assert.Equal(0, Pixels.RootMeanSquaredError(still, firstFrame));
    }

    [Fact]
    public async Task SaveAsWebP_WritesAStillImageWhenEveryStepLastsAnHourOrMore()
    {
        var aniData = Read(AniBuilder.FromFixture().WithRates(216000, 216000, 216000));
        using var directory = new TemporaryDirectory();
        var target = directory.Combine("cursor.webp");

        await aniData.SaveAsWebP(target, aniData.Animations[0]);

        Assert.True(File.Exists(target));
        using var collection = new MagickImageCollection(target);
        Assert.Single(collection);
    }

    [Fact]
    public async Task GetWebpBytes_GivesTheTimeOfAFrameWithoutAnImageToTheFrameBeforeIt()
    {
        var aniData = Read(new AniBuilder()
            .AddFrame(AniBuilder.FixtureFrames[0])
            .AddFrame(AniBuilder.EmptyCursor)
            .AddFrame(AniBuilder.FixtureFrames[2])
            .WithRates(10, 20, 30));

        Assert.Equal(new[] { 500, 500 }, await ExportedDurations(aniData));
    }

    [Fact]
    public async Task GetWebpBytes_GivesTheTimeOfALeadingFrameWithoutAnImageToTheFrameAfterIt()
    {
        var aniData = Read(new AniBuilder()
            .AddFrame(AniBuilder.EmptyCursor)
            .AddFrame(AniBuilder.FixtureFrames[0])
            .AddFrame(AniBuilder.FixtureFrames[2])
            .WithRates(20, 10, 30));

        Assert.Equal(new[] { 500, 500 }, await ExportedDurations(aniData));
    }

    /// <summary>
    /// Two 166.66 ms steps merged into one frame last 333.33 ms, which rounds to 330 ms. Rounding each step before
    /// adding them would give 340 ms.
    /// </summary>
    [Fact]
    public async Task GetWebpBytes_RoundsAMergedDurationOnce()
    {
        var aniData = Read(new AniBuilder()
            .AddFrame(AniBuilder.FixtureFrames[0])
            .AddFrame(AniBuilder.EmptyCursor)
            .AddFrame(AniBuilder.FixtureFrames[2])
            .WithRates(10, 10, 10));

        Assert.Equal(new[] { 330, 170 }, await ExportedDurations(aniData));
    }

    private static AniData Read(AniBuilder builder) =>
        Assert.Single(new AniReader().Read(builder.Build())!);

    private static async Task<IEnumerable<int>> ExportedDurations(AniData aniData) =>
        WebPAnimation.ReadFrameInfo((await aniData.GetWebpBytes(aniData.Animations[0]))!)
            .Select(frame => frame.DurationMilliseconds);
}
