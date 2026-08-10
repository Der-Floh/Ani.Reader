namespace Ani.Reader.Test.Fixtures;

internal static class TestFiles
{
    public const string AnimatedThreeFrame = "test-anim-t.ani";
    public const string StaticBig = "test-static-big.cur";
    public const string StaticBigTransparent = "test-static-big-t.cur";
    public const string StaticMultiSize = "test-static-small-multi.cur";
    public const string StaticMultiSizeTransparent = "test-static-small-multi-t.cur";

    public static IEnumerable<string> CursorFiles =>
        [StaticBig, StaticBigTransparent, StaticMultiSize, StaticMultiSizeTransparent];

    public static string ResourceDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Resources");

    public static string GoldenDirectory { get; } = Path.Combine(ResourceDirectory, "Golden");

    public static string PathTo(string fileName) => Path.Combine(ResourceDirectory, fileName);

    public static byte[] BytesOf(string fileName) => File.ReadAllBytes(PathTo(fileName));

    /// <summary>
    /// Resolves <c>Ani.Reader.Test/Resources</c> inside the source tree rather than the build output,
    /// so regenerated golden images land next to the fixtures that are committed.
    /// </summary>
    public static string SourceResourceDirectory
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "Ani.Reader.Test.csproj");
                if (File.Exists(candidate))
                    return Path.Combine(directory.FullName, "Resources");

                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                $"Could not locate Ani.Reader.Test.csproj above '{AppContext.BaseDirectory}'.");
        }
    }
}
