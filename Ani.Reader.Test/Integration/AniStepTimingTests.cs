using Ani.Reader.Test.Fixtures;

using Xunit;

namespace Ani.Reader.Test.Integration;

/// <summary>
/// An animation plays a sequence of steps. The <c>seq </c> chunk names the frame each step shows and the
/// <c>rate</c> chunk how long each step lasts, one entry per step, so a frame shown at several steps can last
/// differently at each of them.
/// </summary>
public sealed class AniStepTimingTests
{
    [Theory]
    [InlineData(new uint[] { 2, 0, 1 }, new uint[] { 10, 20, 30 }, new uint[] { 10, 20, 30 })]
    [InlineData(new uint[] { 0, 1, 0, 2 }, new uint[] { 10, 20, 30, 40 }, new uint[] { 10, 20, 30, 40 })]
    [InlineData(new uint[] { 2, 0, 1 }, new uint[] { 20 }, new uint[] { 20, 10, 10 })]
    public void Frames_TakeTheDurationOfTheirOwnStep(uint[] sequence, uint[] rates, uint[] expectedJiffies)
    {
        var builder = AniBuilder.FromFixture().WithSequence(sequence).WithRates(rates);

        var aniData = Assert.Single(new AniReader().Read(builder.Build())!);

        var expected = expectedJiffies.Select(jiffies => TimeSpan.FromSeconds(jiffies / 60.0)).ToArray();
        Assert.Equal(expected, aniData.Frames.Select(frame => frame.Duration));
        Assert.Equal(StartsOf(expected), aniData.Frames.Select(frame => frame.Start));
        Assert.Equal(expected.Aggregate(TimeSpan.Zero, (total, duration) => total + duration), aniData.TotalAnimationDuration);
        Assert.Equal(
            sequence.Select(frameIndex => builder.FrameOffsets[(int)frameIndex]),
            aniData.Frames.Select(frame => frame.FrameReference.RealOffset));
    }

    private static IEnumerable<TimeSpan> StartsOf(IEnumerable<TimeSpan> durations)
    {
        var start = TimeSpan.Zero;
        foreach (var duration in durations)
        {
            yield return start;
            start += duration;
        }
    }
}
