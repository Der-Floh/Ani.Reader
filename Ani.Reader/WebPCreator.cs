using Ani.Reader.Models;

using ImageMagick;

namespace Ani.Reader;

public static class WebPCreator
{
    private static readonly TimeSpan MaxFrameDuration = TimeSpan.FromHours(1);

    public static async Task SaveAsWebP(this AniData aniData, string path, AnimationInformation aniInfo)
    {
        var outputFile = InitializePath(path, aniData, aniInfo);
        long durationCorrection = 0;
        using var collection = new MagickImageCollection();
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
                AnimationDelay = (uint)(duration.TotalMilliseconds / 10) // WebP Delay in 1/100s
            };
            collection.Add(magickImage);
        }

        collection.Write(outputFile, MagickFormat.WebP);
    }

    public static async Task<byte[]?> GetWebpBytes(this AniData aniData, AnimationInformation aniInfo)
    {
        long durationCorrection = 0;
        using var collection = new MagickImageCollection();
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
                AnimationDelay = (uint)(duration.TotalMilliseconds / 10) // WebP Delay in 1/100s
            };
            collection.Add(magickImage);
        }

        using var memoryStream = new MemoryStream();
        collection.Write(memoryStream, MagickFormat.WebP);
        return memoryStream.ToArray();
    }

    private static string InitializePath(string path, AniData aniData, AnimationInformation aniInfo)
    {
        string filePath;
        var fileAttributes = File.GetAttributes(path);
        filePath = fileAttributes.HasFlag(FileAttributes.Directory)
            ? Path.Combine(path, aniData.Name, $"{aniData.Name} ({aniInfo.Width}x{aniInfo.Height} {aniInfo.BitCount} bit).webp")
            : path;

        var fileDir = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(fileDir))
            Directory.CreateDirectory(fileDir);

        return filePath;
    }
}
