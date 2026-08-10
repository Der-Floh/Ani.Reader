using Ani.Reader.Test.Fixtures;

using ImageMagick;

using Xunit;

namespace Ani.Reader.Test.Imaging;

/// <summary>
/// Golden-image comparison. Goldens capture the output of a known-good build; they are compared by
/// decoded pixels under a tolerance so that a new encoder version in a dependency does not fail the build.
/// <para>
/// Regenerate with <c>ANIREADER_REGENERATE_GOLDEN=1 dotnet test</c>, then review the diff before committing.
/// </para>
/// </summary>
internal static class Golden
{
    /// <summary>Lossless round trip: identical pixels are expected, this only absorbs rounding.</summary>
    public const double LosslessTolerance = 0.0001;

    public static bool Regenerating =>
        Environment.GetEnvironmentVariable("ANIREADER_REGENERATE_GOLDEN") is "1" or "true";

    public static void VerifyImage(string relativePath, byte[] actual, double tolerance)
    {
        if (Regenerating)
        {
            Write(relativePath, actual);
            return;
        }

        using var expected = new MagickImage(Read(relativePath));
        using var produced = new MagickImage(actual);

        Assert.Equal(expected.Width, produced.Width);
        Assert.Equal(expected.Height, produced.Height);

        var error = Pixels.RootMeanSquaredError(produced, expected);
        Assert.True(
            error <= tolerance,
            $"'{relativePath}' differs from the golden image (RMSE {error:F6} > {tolerance:F6}).");
    }

    public static void VerifyAnimation(string relativePath, byte[] actual, int expectedFrameCount, double tolerance)
    {
        if (Regenerating)
        {
            Write(relativePath, actual);
            return;
        }

        var expectedBytes = Read(relativePath);
        var expected = WebPAnimation.Composite(expectedBytes);
        var produced = WebPAnimation.Composite(actual);

        try
        {
            Assert.Equal(expectedFrameCount, produced.Count);
            Assert.Equal(expected.Count, produced.Count);
            Assert.Equal(
                WebPAnimation.ReadFrameInfo(expectedBytes).Select(f => f.DurationMilliseconds),
                WebPAnimation.ReadFrameInfo(actual).Select(f => f.DurationMilliseconds));

            for (var i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].Width, produced[i].Width);
                Assert.Equal(expected[i].Height, produced[i].Height);

                var error = Pixels.RootMeanSquaredError(produced[i], expected[i]);
                Assert.True(
                    error <= tolerance,
                    $"'{relativePath}' frame {i} differs from the golden image (RMSE {error:F6} > {tolerance:F6}).");
            }
        }
        finally
        {
            expected.ForEach(image => image.Dispose());
            produced.ForEach(image => image.Dispose());
        }
    }

    private static byte[] Read(string relativePath)
    {
        var path = Path.Combine(TestFiles.GoldenDirectory, relativePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Missing golden image '{relativePath}'. Regenerate with " +
                "ANIREADER_REGENERATE_GOLDEN=1 dotnet test, then review the result before committing.",
                path);
        }

        return File.ReadAllBytes(path);
    }

    private static void Write(string relativePath, byte[] content)
    {
        var path = Path.Combine(TestFiles.SourceResourceDirectory, "Golden", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }
}
