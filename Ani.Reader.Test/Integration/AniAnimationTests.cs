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
    /// The 64x64 variant is PNG-encoded and each frame stores it at whatever palette depth that frame needs, so
    /// the per-frame reported bit depths differ. Variant identity is size-based, so it survives regardless.
    /// </summary>
    [Fact]
    public void Animations_KeepTheVariantWhoseReportedBitDepthVariesPerFrame()
    {
        var aniData = Load();

        var reportedBitDepths = aniData.Frames
            .Select(f => f.VariationDetails.Single(v => v.Width == 64).BitCount)
            .Distinct();
        Assert.True(reportedBitDepths.Count() > 1, "the fixture no longer varies its 64x64 bit depth per frame");

        Assert.Contains(aniData.Animations, a => a.Width == 64);
    }

    [Fact]
    public void Animations_ReportOneVariantPerSize()
    {
        var aniData = Load();

        Assert.Equal(
            aniData.Animations.Select(a => (a.Width, a.Height)).Distinct().Count(),
            aniData.Animations.Count);
    }

    [Fact]
    public void Animations_ReportTheDepthTheConsumerReceives()
    {
        var aniData = Load();

        Assert.All(aniData.Animations, animation => Assert.Equal(32, animation.BitCount));
    }

    [Fact]
    public void PreferredAnimationIndex_PicksTheLargestVariant()
    {
        var aniData = Load();

        var preferred = aniData.Animations[aniData.PreferredAnimationIndex()];

        Assert.Equal(64, preferred.Width);
        Assert.Equal(aniData.Animations.Max(a => a.Width), preferred.Width);
    }

    /// <summary>
    /// Every variant of this fixture decodes at the same depth, so no weighting can favor a smaller one.
    /// </summary>
    [Theory]
    [InlineData(1f, 2f)]
    [InlineData(1f, 1f)]
    [InlineData(2f, 1f)]
    [InlineData(0f, 1f)]
    public void PreferredAnimationIndex_PicksTheLargestVariantUnderAnyWeighting(float colorBitWeight, float areaWeight)
    {
        var aniData = Load();

        var preferred = aniData.Animations[aniData.PreferredAnimationIndex(colorBitWeight, areaWeight)];

        Assert.Equal(64, preferred.Width);
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

    [Fact]
    public void FrameHotspots_NumberTheFramesInOrder()
    {
        var aniData = Load();

        foreach (var animation in aniData.Animations)
        {
            Assert.Equal(
                Enumerable.Range(0, Fixture.FrameCount),
                animation.FrameHotspots.Select(hotspot => hotspot.FramePosition));
        }
    }
}
