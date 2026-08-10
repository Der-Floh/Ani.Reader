using ImageMagick;

namespace Ani.Reader.Test.Imaging;

internal static class Pixels
{
    public static MagickImage Decode(byte[] data) => new(data);

    public static int CountTransparent(IMagickImage<ushort> image) =>
        CountWhere(image, color => color.A == 0);

    public static int CountOpaque(IMagickImage<ushort> image) =>
        CountWhere(image, color => color.A == ushort.MaxValue);

    public static int CountMatching(IMagickImage<ushort> image, IMagickColor<ushort> expected) =>
        CountWhere(image, color => color.A == ushort.MaxValue && SameRgb(color, expected));

    public static IMagickColor<ushort> At(IMagickImage<ushort> image, int x, int y)
    {
        using var pixels = image.GetPixels();
        return pixels.GetPixel(x, y).ToColor()
            ?? throw new InvalidOperationException($"No pixel at {x},{y}.");
    }

    public static IMagickColor<ushort> Center(IMagickImage<ushort> image) =>
        At(image, (int)image.Width / 2, (int)image.Height / 2);

    public static IReadOnlyCollection<string> DistinctColors(IMagickImage<ushort> image)
    {
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        ForEachPixel(image, color => distinct.Add(Describe(color)));
        return distinct;
    }

    public static string Describe(IMagickColor<ushort> color) =>
        $"#{color.R:X4}{color.G:X4}{color.B:X4}a{color.A:X4}";

    /// <summary>
    /// Compares two images by decoded pixel content. Encoded bytes are deliberately not compared:
    /// the PNG and WebP encoders live in dependencies whose output may change without a behaviour change.
    /// </summary>
    public static double RootMeanSquaredError(IMagickImage<ushort> actual, IMagickImage<ushort> expected)
    {
        using var left = CloneWithAlpha(actual);
        using var right = CloneWithAlpha(expected);
        return left.Compare(right, ErrorMetric.RootMeanSquared);
    }

    private static MagickImage CloneWithAlpha(IMagickImage<ushort> image)
    {
        var clone = new MagickImage(image);
        clone.Alpha(AlphaOption.Set);
        return clone;
    }

    private static bool SameRgb(IMagickColor<ushort> left, IMagickColor<ushort> right) =>
        left.R == right.R && left.G == right.G && left.B == right.B;

    private static int CountWhere(IMagickImage<ushort> image, Func<IMagickColor<ushort>, bool> predicate)
    {
        var count = 0;
        ForEachPixel(image, color =>
        {
            if (predicate(color))
                count++;
        });

        return count;
    }

    private static void ForEachPixel(IMagickImage<ushort> image, Action<IMagickColor<ushort>> action)
    {
        using var pixels = image.GetPixels();
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var color = pixels.GetPixel(x, y).ToColor();
                if (color is not null)
                    action(color);
            }
        }
    }
}
