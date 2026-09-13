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
        Assert.Equal(0, AniData.BestByQuality(BigShallowVersusSmallDeep, colorBitWeight: 1, areaWeight: 4));
    }

    [Fact]
    public void FavoringColorDepth_PicksTheDeeperVariant()
    {
        Assert.Equal(1, AniData.BestByQuality(BigShallowVersusSmallDeep, colorBitWeight: 4, areaWeight: 1));
    }

    /// <summary>
    /// The weights come in the order <see cref="Ico.Reader.Data.IcoData.PreferredImageIndex(float, float)"/> takes them.
    /// </summary>
    [Fact]
    public void PositionalWeights_TakeTheColorBitWeightFirst()
    {
        Assert.Equal(1, AniData.BestByQuality(BigShallowVersusSmallDeep, 4f, 1f));
    }

    /// <summary>
    /// The weights are a ratio, so scaling both must not change the outcome. Ico.Reader's original
    /// implementation multiplied the whole product by the weight, which could never reorder anything.
    /// </summary>
    [Theory]
    [InlineData(1f, 2f)]
    [InlineData(10f, 20f)]
    [InlineData(0.333f, 0.667f)]
    public void EquivalentWeightRatios_RankIdentically(float colorBitWeight, float areaWeight)
    {
        var expected = AniData.BestByQuality(BigShallowVersusSmallDeep, colorBitWeight: 1, areaWeight: 2);

        Assert.Equal(expected, AniData.BestByQuality(BigShallowVersusSmallDeep, colorBitWeight, areaWeight));
    }

    [Fact]
    public void ZeroColorBitWeight_RanksByAreaAlone()
    {
        AnimationInformation[] variants = [Variant(32, 32), Variant(64, 1)];

        Assert.Equal(1, AniData.BestByQuality(variants, colorBitWeight: 0, areaWeight: 1));
    }

    [Fact]
    public void ZeroAreaWeight_RanksByColorDepthAlone()
    {
        AnimationInformation[] variants = [Variant(32, 32), Variant(64, 1)];

        Assert.Equal(0, AniData.BestByQuality(variants, colorBitWeight: 1, areaWeight: 0));
    }

    [Fact]
    public void EqualCandidates_PickTheFirst()
    {
        AnimationInformation[] variants = [Variant(32, 32), Variant(32, 32)];

        Assert.Equal(0, AniData.BestByQuality(variants, colorBitWeight: 1, areaWeight: 2));
    }

    [Fact]
    public void NoVariants_ReturnsMinusOne()
    {
        Assert.Equal(-1, AniData.BestByQuality([], colorBitWeight: 1, areaWeight: 2));
        Assert.Equal(-1, AniData.BestByQuality(null, colorBitWeight: 1, areaWeight: 2));
    }

    [Fact]
    public void AllDepthsZero_StillRanksByArea()
    {
        AnimationInformation[] variants = [Variant(32, 0), Variant(64, 0)];

        Assert.Equal(1, AniData.BestByQuality(variants, colorBitWeight: 1, areaWeight: 2));
    }

    [Theory]
    [InlineData(-1f, 1f)]
    [InlineData(1f, -1f)]
    public void NegativeWeights_Throw(float colorBitWeight, float areaWeight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AniData.BestByQuality(BigShallowVersusSmallDeep, colorBitWeight, areaWeight));
    }

    [Fact]
    public void ZeroWeights_Throw()
    {
        Assert.Throws<ArgumentException>(() => AniData.BestByQuality(BigShallowVersusSmallDeep, 0, 0));
    }
}
