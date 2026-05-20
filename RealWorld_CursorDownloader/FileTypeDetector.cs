namespace RealWorldDownloader;

/// <summary>
/// Detects common file types from magic bytes.
/// Covers the formats typically found on rw-designer.com: ani, cur, ico, png, gif, bmp, jpg, zip.
/// </summary>
internal static class FileTypeDetector
{
    // Each entry: (magic bytes, byte offset, extension)
    private static readonly (byte[] Magic, int Offset, string Extension)[] Signatures =
    [
        ([0x89, 0x50, 0x4E, 0x47], 0, "png"),          // PNG
        ([0xFF, 0xD8, 0xFF],        0, "jpg"),          // JPEG
        ([0x47, 0x49, 0x46, 0x38], 0, "gif"),          // GIF87a / GIF89a
        ([0x42, 0x4D],              0, "bmp"),          // BMP
        ([0x00, 0x00, 0x01, 0x00], 0, "ico"),          // ICO
        ([0x00, 0x00, 0x02, 0x00], 0, "cur"),          // CUR
        ([0x50, 0x4B, 0x03, 0x04], 0, "zip"),          // ZIP
    ];

    // RIFF FourCCs at offset 8 that identify ANI (animated cursor)
    private static readonly byte[] RiffMagic = [0x52, 0x49, 0x46, 0x46]; // "RIFF"
    private static readonly byte[] AconFourCC = [0x41, 0x43, 0x4F, 0x4E]; // "ACON"

    /// <summary>Returns the lowercase file extension for <paramref name="data"/>, or "unknown".</summary>
    public static string Detect(byte[] data)
    {
        if (data is null || data.Length < 4)
            return "unknown";

        // RIFF container: check FourCC at offset 8 for "ACON" → animated cursor
        if (data.Length >= 12 && MatchAt(data, RiffMagic, 0) && MatchAt(data, AconFourCC, 8))
            return "ani";

        foreach (var (magic, offset, ext) in Signatures)
        {
            if (data.Length >= offset + magic.Length && MatchAt(data, magic, offset))
                return ext;
        }

        return "unknown";
    }

    private static bool MatchAt(byte[] data, byte[] magic, int offset)
    {
        for (int i = 0; i < magic.Length; i++)
        {
            if (data[offset + i] != magic[i])
                return false;
        }
        return true;
    }
}
