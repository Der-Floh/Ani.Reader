using Ani.Reader.Models;

using Xunit;

namespace Ani.Reader.Test.Unit;

public sealed class AnimationInformationTests
{
    private static AnimationInformation Create(int width, int height, int bitCount) =>
        new() { Width = width, Height = height, BitCount = bitCount };

    [Fact]
    public void Equality_ComparesSizeAndBitDepth()
    {
        var left = Create(32, 32, 32);
        var right = Create(32, 32, 32);

        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Theory]
    [InlineData(48, 32, 32)]
    [InlineData(32, 48, 32)]
    [InlineData(32, 32, 24)]
    public void Equality_DistinguishesEveryComponent(int width, int height, int bitCount)
    {
        var left = Create(32, 32, 32);
        var right = Create(width, height, bitCount);

        Assert.False(left == right);
        Assert.True(left != right);
        Assert.NotEqual(left, right);
    }

    /// <summary>
    /// Hotspots are per-frame data hanging off the variant, not part of its identity.
    /// </summary>
    [Fact]
    public void Equality_IgnoresFrameHotspots()
    {
        var left = Create(32, 32, 32);
        var right = Create(32, 32, 32);
        left.FrameHotspots = [new FrameHotspot { FramePosition = 0, HotspotX = 1, HotspotY = 2 }];
        right.FrameHotspots = [new FrameHotspot { FramePosition = 0, HotspotX = 9, HotspotY = 9 }];

        Assert.Equal(left, right);
    }

    [Fact]
    public void Equality_HandlesNullAndSelf()
    {
        var value = Create(32, 32, 32);
        var alias = value;

        Assert.True(value == alias);
        Assert.False(value == null!);
        Assert.False(null! == value);
        Assert.True(null! == (AnimationInformation)null!);
        Assert.False(value.Equals("not an animation"));
    }
}
