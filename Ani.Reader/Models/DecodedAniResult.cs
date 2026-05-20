namespace Ani.Reader.Models;

/// <summary>
/// Represents the result of decoding ANI data from a file or PE resource.
/// </summary>
public sealed class DecodedAniResult
{
    public AniOriginFileType OriginFileType { get; set; }
    public List<AniEntry> Entries { get; set; } = [];
}
