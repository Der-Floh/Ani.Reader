using Ani.Reader.Models;

namespace Ani.Reader;

/// <summary>
/// Decides whether Windows loads an animation and which frame each step shows for how long: the steps Windows plays
/// when it loads the animation, and otherwise as many steps as the file describes.
/// </summary>
internal static class AniPlayback
{
    private const uint HeaderSize = 36;

    public static bool LoadsOnWindows(AniEntry entry, Func<int, bool> frameHoldsImage)
    {
        var header = entry.Header;
        if (!entry.ChunkLayoutLoadsOnWindows || header.HeaderSize != HeaderSize)
            return false;

        if (header.NumFrames == 0 || header.NumFrames > entry.Frames.Count || header.NumSteps == 0)
            return false;

        var sequence = entry.FrameSequence;
        var sequenceFits = sequence.Count == 0
            ? header.NumSteps <= header.NumFrames
            : sequence.Count == header.NumSteps && sequence.All(frame => frame < header.NumFrames);

        var ratesFit = entry.FrameRates.Count == 0
            ? header.DisplayRate > 0
            : entry.FrameRates.Count == header.NumSteps;

        return sequenceFits && ratesFit && Enumerable.Range(0, (int)header.NumFrames).All(frameHoldsImage);
    }

    public static IReadOnlyList<(int FrameIndex, uint Rate)> Steps(AniEntry entry, bool loadsOnWindows)
    {
        if (entry.Frames.Count == 0)
            return [];

        var sequence = entry.FrameSequence;
        var stepCount = loadsOnWindows
            ? (int)entry.Header.NumSteps
            : sequence.Count > 0 ? sequence.Count : entry.Frames.Count;

        var lastFrame = (uint)entry.Frames.Count - 1;
        var steps = new (int FrameIndex, uint Rate)[stepCount];
        for (var i = 0; i < stepCount; i++)
        {
            var frameIndex = i < sequence.Count ? (int)Math.Min(sequence[i], lastFrame) : i;
            var rate = i < entry.FrameRates.Count ? entry.FrameRates[i] : entry.Header.DisplayRate;
            steps[i] = (frameIndex, rate);
        }

        return steps;
    }
}
