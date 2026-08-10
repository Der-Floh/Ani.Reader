using Ani.Reader.Models;
using Ani.Reader.Test.Fixtures;

using Xunit;

namespace Ani.Reader.Test.Integration;

public sealed class AniAnimationTests
{
    private static readonly CursorFixture Fixture = CursorFixtures.For(TestFiles.AnimatedThreeFrame);

    private static AniData Load() =>
        new AniReader().Read(Fixture.Path)?[0]
            ?? throw new InvalidOperationException($"{Fixture.FileName} failed to read.");

    [Fact]
    public void Frames_MatchTheFixtureCount()
    {
        var aniData = Load();

        Assert.Equal(Fixture.FrameCount, aniData.TotalFrames);
        Assert.Equal(Fixture.FrameCount, aniData.Frames.Count);
        Assert.Equal(Enumerable.Range(0, Fixture.FrameCount), aniData.Frames.Select(f => f.Position));
    }

    [Fact]
    public void Frames_RunBackToBackAtTheDeclaredRate()
    {
        var aniData = Load();

        Assert.All(aniData.Frames, frame => Assert.Equal(Fixture.FrameDuration, frame.Duration));
        Assert.Equal(
            Enumerable.Range(0, Fixture.FrameCount).Select(i => Fixture.FrameDuration * i),
            aniData.Frames.Select(f => f.Start));
    }

    [Fact]
    public void TotalAnimationDuration_IsTheSumOfEveryFrame()
    {
        var aniData = Load();

        Assert.Equal(Fixture.FrameDuration * Fixture.FrameCount, aniData.TotalAnimationDuration);
    }

    [Fact]
    public void FrameRate_IsDerivedFromTheDisplayRate()
    {
        var aniData = Load();

        Assert.Equal(60f / Fixture.DisplayRateJiffies, aniData.FrameRate);
    }

    [Fact]
    public void VariationDetails_ExposeEverySizeWithItsHotspot()
    {
        var aniData = Load();

        foreach (var frame in aniData.Frames)
        {
            var variations = frame.VariationDetails.OrderBy(v => v.Width).ToArray();

            Assert.Equal(Fixture.Sizes.Count, variations.Length);
            Assert.Equal(Fixture.Sizes.Select(s => s.Width), variations.Select(v => v.Width));
            Assert.Equal(Fixture.Sizes.Select(s => s.Height), variations.Select(v => v.Height));
            Assert.Equal(Fixture.Sizes.Select(s => s.HotspotX), variations.Select(v => v.HotspotX));
            Assert.Equal(Fixture.Sizes.Select(s => s.HotspotY), variations.Select(v => v.HotspotY));
        }
    }

    [Fact]
    public void Animations_ExposeTheVariantsTheFixtureMarksAsReachable()
    {
        var aniData = Load();
        var expected = Fixture.Sizes.Where(s => s.ReachableAsAnimation).ToArray();

        Assert.Equal(expected.Length, aniData.Animations.Count);
        Assert.Equal(expected.Select(s => s.Width), aniData.Animations.Select(a => a.Width));
        Assert.Equal(expected.Select(s => s.BitCount), aniData.Animations.Select(a => (int?)a.BitCount));
    }

    /// <summary>
    /// Every frame carries a 64x64 variant, but <see cref="AniData.Animations"/> drops it: the variants are
    /// intersected across frames by width/height/bit depth, and Ico.Reader reports a different bit depth for
    /// the PNG-encoded 64x64 entry on each frame (3 vs 12), so the intersection removes it.
    /// </summary>
    [Fact]
    public void Animations_DropTheVariantWhoseReportedBitDepthVariesPerFrame()
    {
        var aniData = Load();
        var missing = Fixture.Sizes.Single(s => !s.ReachableAsAnimation);

        Assert.All(
            aniData.Frames,
            frame => Assert.Contains(frame.VariationDetails, v => v.Width == missing.Width));
        Assert.DoesNotContain(aniData.Animations, a => a.Width == missing.Width);

        var reportedBitDepths = aniData.Frames
            .Select(f => f.VariationDetails.Single(v => v.Width == missing.Width).BitCount)
            .Distinct();
        Assert.True(reportedBitDepths.Count() > 1);
    }

    [Fact]
    public void PreferredAnimationIndex_PicksTheLargestReachableVariant()
    {
        var aniData = Load();

        var preferred = aniData.Animations[aniData.PreferredAnimationIndex()];

        Assert.Equal(aniData.Animations.Max(a => a.Width), preferred.Width);
    }

    [Fact]
    public void FrameHotspots_UseTheHotspotOfTheirVariant()
    {
        var aniData = Load();

        foreach (var animation in aniData.Animations)
        {
            var expected = Fixture.SizeOf(animation.Width);
            var hotspots = animation.FrameHotspots.ToArray();

            Assert.Equal(Fixture.FrameCount, hotspots.Length);
            Assert.All(hotspots, hotspot =>
            {
                Assert.Equal(expected.HotspotX, hotspot.HotspotX);
                Assert.Equal(expected.HotspotY, hotspot.HotspotY);
            });
        }
    }

    /// <summary>
    /// The loop that fills FrameHotspots never advances its counter, so every entry reports frame 0.
    /// </summary>
    [Fact]
    public void FrameHotspots_AllReportFramePositionZero()
    {
        var aniData = Load();

        foreach (var animation in aniData.Animations)
            Assert.All(animation.FrameHotspots, hotspot => Assert.Equal(0, hotspot.FramePosition));
    }
}
