using Ani.Reader.Models;

using ImageMagick;
using ImageMagick.Formats;

namespace Ani.Reader;

/// <summary>
/// Encodes an animation as an animated, lossless WebP image.
/// </summary>
public static class WebPCreator
{
    private static readonly TimeSpan MaxFrameDuration = TimeSpan.FromHours(1);

    private static readonly WebPWriteDefines WriteDefines = new()
    {
        Lossless = true,
        Exact = true,
    };

    /// <summary>
    /// Writes one size of the animation to an animated WebP file.
    /// </summary>
    /// <param name="aniData">The animation to encode.</param>
    /// <param name="path">
    /// An existing directory, in which case the file is written to a subdirectory named after <see cref="AniData.Name"/>
    /// as <c>Name (WidthxHeight BitCount bit).webp</c>; otherwise the path of the file itself. Missing directories are
    /// created.
    /// </param>
    /// <param name="aniInfo">The size to encode, one of <see cref="AniData.Animations"/>.</param>
    /// <returns>A task that completes once the file has been written.</returns>
    /// <remarks>
    /// Frames shown for an hour or longer are left out together with their time. A frame whose data cannot be read
    /// or holds no image is left out too, but its time goes to the frame before it, or to the frame after it when it
    /// comes first, so the remaining frames keep their place in the animation. Frame durations are rounded to
    /// hundredths of a second. When no frame remains, the first frame that holds an image is written as a still image.
    /// </remarks>
    public static async Task SaveAsWebP(this AniData aniData, string path, AnimationInformation aniInfo)
    {
        var outputFile = InitializePath(path, aniData, aniInfo);
        using var collection = await CreateCollection(aniData, aniInfo);
        collection.Write(outputFile, WriteDefines);
    }

    /// <summary>
    /// Encodes one size of the animation as an animated WebP image in memory.
    /// </summary>
    /// <param name="aniData">The animation to encode.</param>
    /// <param name="aniInfo">The size to encode, one of <see cref="AniData.Animations"/>.</param>
    /// <returns>The encoded WebP image.</returns>
    /// <remarks>
    /// Frames are chosen and timed as described for <see cref="SaveAsWebP"/>.
    /// </remarks>
    public static async Task<byte[]> GetWebpBytes(this AniData aniData, AnimationInformation aniInfo)
    {
        using var collection = await CreateCollection(aniData, aniInfo);
        using var memoryStream = new MemoryStream();
        collection.Write(memoryStream, WriteDefines);
        return memoryStream.ToArray();
    }

    private static async Task<MagickImageCollection> CreateCollection(AniData aniData, AnimationInformation aniInfo)
    {
        var shownFrames = await CollectShownFrames(aniData, aniInfo);

        var collection = new MagickImageCollection();
        foreach (var shownFrame in shownFrames)
        {
            using var stream = new MemoryStream(shownFrame.PngData);
            collection.Add(new MagickImage(stream)
            {
                Format = MagickFormat.Png32,
                BackgroundColor = MagickColors.Transparent,
                AnimationDelay = ToAnimationDelay(shownFrame.Duration),
            });
        }

        return collection;
    }

    private static async Task<List<ShownFrame>> CollectShownFrames(AniData aniData, AnimationInformation aniInfo)
    {
        var shownFrames = new List<ShownFrame>();
        var carriedDuration = TimeSpan.Zero;
        foreach (var frame in aniData.Frames)
        {
            if (frame.Duration >= MaxFrameDuration)
                continue;

            var pngData = await aniData.GetFrameBytes(aniInfo, frame);
            if (pngData is null)
            {
                if (shownFrames.Count == 0)
                    carriedDuration += frame.Duration;
                else
                    shownFrames[shownFrames.Count - 1].Duration += frame.Duration;

                continue;
            }

            shownFrames.Add(new ShownFrame(pngData, frame.Duration + carriedDuration));
            carriedDuration = TimeSpan.Zero;
        }

        if (shownFrames.Count == 0 && await FirstImage(aniData, aniInfo) is { } stillImage)
            shownFrames.Add(new ShownFrame(stillImage, TimeSpan.Zero));

        return shownFrames;
    }

    private static async Task<byte[]?> FirstImage(AniData aniData, AnimationInformation aniInfo)
    {
        foreach (var frame in aniData.Frames)
        {
            if (await aniData.GetFrameBytes(aniInfo, frame) is { } pngData)
                return pngData;
        }

        return null;
    }

    // AnimationDelay counts hundredths of a second at Magick.NET's default AnimationTicksPerSecond.
    private static uint ToAnimationDelay(TimeSpan duration)
        => (uint)Math.Round(duration.TotalMilliseconds / 10, MidpointRounding.AwayFromZero);

    private static string InitializePath(string path, AniData aniData, AnimationInformation aniInfo)
    {
        var filePath = Directory.Exists(path)
            ? Path.Combine(path, aniData.Name, $"{aniData.Name} ({aniInfo.Width}x{aniInfo.Height} {aniInfo.BitCount} bit).webp")
            : path;

        var fileDir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(fileDir))
            Directory.CreateDirectory(fileDir);

        return filePath;
    }

    private sealed class ShownFrame
    {
        public ShownFrame(byte[] pngData, TimeSpan duration)
        {
            PngData = pngData;
            Duration = duration;
        }

        public byte[] PngData { get; }

        public TimeSpan Duration { get; set; }
    }
}
