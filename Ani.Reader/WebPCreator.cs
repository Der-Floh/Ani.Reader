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
    /// Frames whose data cannot be read, and frames shown for an hour or longer, are left out. Frame durations are
    /// rounded to hundredths of a second.
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
    public static async Task<byte[]?> GetWebpBytes(this AniData aniData, AnimationInformation aniInfo)
    {
        using var collection = await CreateCollection(aniData, aniInfo);
        using var memoryStream = new MemoryStream();
        collection.Write(memoryStream, WriteDefines);
        return memoryStream.ToArray();
    }

    private static async Task<MagickImageCollection> CreateCollection(AniData aniData, AnimationInformation aniInfo)
    {
        long durationCorrection = 0;
        var collection = new MagickImageCollection();
        foreach (var frame in aniData.Frames)
        {
            var duration = frame.Duration;
            if (frame.Duration >= MaxFrameDuration)
            {
                durationCorrection += frame.Duration.Ticks;
                continue;
            }
            else if (durationCorrection != 0)
            {
                duration -= TimeSpan.FromTicks(durationCorrection);
            }

            var frameChunk = frame.FrameReference.GetFrameStream(aniData.DataSource.GetStream());
            var icoData = aniData.Reader.Read(frameChunk);
            if (icoData is null)
            {
                durationCorrection += frame.Duration.Ticks;
                continue;
            }

            if (icoData.ImageReferences.Count == 0) // Possibly make available via config?
            {
                durationCorrection += frame.Duration.Ticks;
                continue;
            }

            var imageReference = aniData.FindByAnimationInformation(icoData, aniInfo);
            var pngData = await icoData.GetImageAsync(imageReference);
            using var ms = new MemoryStream(pngData);
            var magickImage = new MagickImage(ms)
            {
                Format = MagickFormat.Png32,
                BackgroundColor = MagickColors.Transparent,
                AnimationDelay = (uint)Math.Round(duration.TotalMilliseconds / 10, MidpointRounding.AwayFromZero) // WebP Delay in 1/100s
            };
            collection.Add(magickImage);
        }

        return collection;
    }

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
}
