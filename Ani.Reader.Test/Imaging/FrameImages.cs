using Ani.Reader.Models;

namespace Ani.Reader.Test.Imaging;

internal static class FrameImages
{
    /// <summary>
    /// Extracts one size variant of a frame through the low-level reference API, matching on dimensions only.
    /// <see cref="AniData.GetFrameBytes"/> cannot be used for every size because
    /// <see cref="AniData.FindByAnimationInformation"/> also matches on bit depth.
    /// </summary>
    public static async Task<byte[]> ExtractAsync(AniData aniData, FrameInformation frame, int width)
    {
        using var baseStream = aniData.DataSource.GetStream();
        using var chunk = frame.FrameReference.GetFrameStream(baseStream);

        var icoData = aniData.Reader.Read(chunk)
            ?? throw new InvalidOperationException($"Frame {frame.Position} could not be read as cursor data.");

        var reference = icoData.ImageReferences.FirstOrDefault(r => r.Width == width)
            ?? throw new InvalidOperationException($"Frame {frame.Position} has no {width}x variant.");

        return await icoData.GetImageAsync(reference);
    }
}
