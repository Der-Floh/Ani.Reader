using Ani.Reader.Decoder;
using Ani.Reader.Test.Fixtures;

using Xunit;

namespace Ani.Reader.Test.Unit;

/// <summary>
/// Positive controls for <see cref="AniBuilder"/>, so a test built on it fails because of the reader and not
/// because of the bytes it was given.
/// </summary>
public sealed class AniBuilderTests
{
    [Fact]
    public void Build_FromTheFixtureFrames_ReproducesTheFixture()
        => Assert.Equal(TestFiles.BytesOf(TestFiles.AnimatedThreeFrame), AniBuilder.FromFixture().Build());

    [Fact]
    public void Build_RecordsWhereEachFrameStarts()
    {
        var builder = AniBuilder.FromFixture().WithRates(10, 20, 30).WithSequence(2, 0, 1);
        using var stream = new MemoryStream(builder.Build());

        var entry = new AniDecoder().Read(stream)
            ?? throw new InvalidOperationException("The built animation failed to decode.");

        Assert.Equal(builder.FrameOffsets, entry.Frames.Select(frame => frame.Offset));
        Assert.Equal([10u, 20u, 30u], entry.FrameRates);
        Assert.Equal([2u, 0u, 1u], entry.FrameSequence);
    }
}
