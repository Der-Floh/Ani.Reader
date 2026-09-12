using Ani.Reader.Models;

using Xunit;

namespace Ani.Reader.Test.Unit;

/// <summary>
/// Covers the variant ranking behind <see cref="AniData.PreferredAnimationIndex"/> directly, because no fixture
/// stores the same animation at genuinely different color depths.
/// </summary>
public sealed class QualityRankingTests
{
    private static AnimationInformation Variant(int size, int bitCount) =>
        new() { Width = size, Height = size, BitCount = bitCount };

    private static readonly AnimationInformation[] BigShallowVersusSmallDeep =
    [
        Variant(64, 4),
        Variant(48, 32),
    ];

    [Fact]
    public void FavoringArea_PicksTheLargerVariant()
    {
        Assert.Equal(0, AniData.BestByQuality(BigShallowVersusSmallDeep, areaWeight: 4, colorBitWeight: 1));
    }

    [Fact]
    public void FavoringColorDepth_PicksTheDeeperVariant()
    {
        Assert.Equal(1, AniData.BestByQuality(BigShallowVersusSmallDeep, areaWeight: 1, colorBitWeight: 4));
    }

    /// <summary>
    /// The weights are a ratio, so scaling both must not change the outcome. Ico.Reader's original
    /// implementation multiplied the whole product by the weight, which could never reorder anything.
    /// </summary>
    [Theory]
    [InlineData(2, 1)]
    [InlineData(20, 10)]
    [InlineData(0.667, 0.333)]
    public void EquivalentWeightRatios_RankIdentically(double areaWeight, double colorBitWeight)
    {
        var expected = AniData.BestByQuality(BigShallowVersusSmallDeep, 2, 1);

        Assert.Equal(expected, AniData.BestByQuality(BigShallowVersusSmallDeep, areaWeight, colorBitWeight));
    }

    [Fact]
    public void ZeroColorBitWeight_RanksByAreaAlone()
    {
        AnimationInformation[] variants = [Variant(32, 32), Variant(64, 1)];

        Assert.Equal(1, AniData.BestByQuality(variants, areaWeight: 1, colorBitWeight: 0));
    }

    [Fact]
    public void ZeroAreaWeight_RanksByColorDepthAlone()
    {
        AnimationInformation[] variants = [Variant(32, 32), Variant(64, 1)];

        Assert.Equal(0, AniData.BestByQuality(variants, areaWeight: 0, colorBitWeight: 1));
    }

    [Fact]
    public void EqualCandidates_PickTheFirst()
    {
        AnimationInformation[] variants = [Variant(32, 32), Variant(32, 32)];

        Assert.Equal(0, AniData.BestByQuality(variants, 2, 1));
    }

    [Fact]
    public void NoVariants_ReturnsMinusOne()
    {
        Assert.Equal(-1, AniData.BestByQuality([], 2, 1));
        Assert.Equal(-1, AniData.BestByQuality(null, 2, 1));
    }

    [Fact]
    public void AllDepthsZero_StillRanksByArea()
    {
        AnimationInformation[] variants = [Variant(32, 0), Variant(64, 0)];

        Assert.Equal(1, AniData.BestByQuality(variants, 2, 1));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void NegativeWeights_Throw(double areaWeight, double colorBitWeight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AniData.BestByQuality(BigShallowVersusSmallDeep, areaWeight, colorBitWeight));
    }

    [Fact]
    public void ZeroWeights_Throw()
    {
        Assert.Throws<ArgumentException>(() => AniData.BestByQuality(BigShallowVersusSmallDeep, 0, 0));
    }
}
