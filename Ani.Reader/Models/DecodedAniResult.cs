namespace Ani.Reader.Models;

/// <summary>
/// Represents the result of decoding ANI data from a file or PE resource.
/// </summary>
public sealed class DecodedAniResult
{
    /// <summary>
    /// The kind of file the animations were read from.
    /// </summary>
    public AniOriginFileType OriginFileType { get; set; }

    /// <summary>
    /// The animations found: one for a standalone .ani file, or one per animated cursor resource in an executable or
    /// DLL.
    /// </summary>
    public List<AniEntry> Entries { get; set; } = [];
}
