using System.Text.Json;
using System.Text.Json.Serialization;

using Ico.Reader.Data;

using ImageMagick;

namespace Ani.Reader.Test.Fixtures;

internal sealed record FixtureSize
{
    public int Width { get; init; }
    public int Height { get; init; }
    public ushort HotspotX { get; init; }
    public ushort HotspotY { get; init; }
    public string Encoding { get; init; } = string.Empty;
    public int? BitCount { get; init; }

    /// <summary>
    /// Whether this size is exposed as an <see cref="Models.AnimationInformation"/> by <see cref="Models.AniData"/>.
    /// </summary>
    public bool ReachableAsAnimation { get; init; }

    public IReadOnlyList<int> TransparentPixelsPerFrame { get; init; } = [];

    public IcoImageFormat ExpectedFormat =>
        Encoding switch
        {
            "PNG" => IcoImageFormat.PNG,
            "BMP" => IcoImageFormat.BMP,
            _ => throw new NotSupportedException($"Unknown encoding '{Encoding}'."),
        };

    public override string ToString() => $"{Width}x{Height}";
}

internal sealed record CursorFixture
{
    public string FileName { get; init; } = string.Empty;
    public bool IsAnimated { get; init; }
    public int FrameCount { get; init; }
    public uint DisplayRateJiffies { get; init; }
    public IReadOnlyList<int> TransparentFrames { get; init; } = [];
    public IReadOnlyList<FixtureSize> Sizes { get; init; } = [];

    public string Path => TestFiles.PathTo(FileName);

    public TimeSpan FrameDuration => TimeSpan.FromSeconds(DisplayRateJiffies / 60.0);

    public FixtureSize SizeOf(int width) =>
        Sizes.FirstOrDefault(s => s.Width == width)
            ?? throw new InvalidOperationException($"{FileName} has no {width}x fixture entry.");

    public override string ToString() => FileName;
}

internal sealed record FixturePalette
{
    public string Foreground { get; init; } = string.Empty;
    public string Background { get; init; } = string.Empty;
}

internal static class CursorFixtures
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private static readonly Lazy<FixtureDocument> Document = new(Load);

    public static MagickColor Foreground => new(Document.Value.Colors.Foreground);

    public static MagickColor Background => new(Document.Value.Colors.Background);

    public static CursorFixture For(string fileName) =>
        Document.Value.Files.FirstOrDefault(f => string.Equals(f.FileName, fileName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No fixture entry for '{fileName}' in fixtures.json.");

    private static FixtureDocument Load()
    {
        var path = TestFiles.PathTo("fixtures.json");
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<FixtureDocument>(stream, SerializerOptions)
            ?? throw new InvalidOperationException($"'{path}' deserialized to null.");
    }

    private sealed record FixtureDocument
    {
        public FixturePalette Colors { get; init; } = new();
        public IReadOnlyList<CursorFixture> Files { get; init; } = [];
    }
}
