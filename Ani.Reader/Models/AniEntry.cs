namespace Ani.Reader.Models;

/// <summary>
/// Represents raw parsed ANI animation data extracted from a RIFF stream.
/// </summary>
public sealed class AniEntry
{
    public Dictionary<string, string> MetaData { get; set; } = [];
    public AniHeader Header { get; set; } = null!;
    public List<uint> FrameRates { get; set; } = [];
    public List<uint> FrameSequence { get; set; } = [];
    public List<AniFrameReference> Frames { get; set; } = [];

    public long EntryOffset { get; set; }
    public long EntrySize { get; set; }
    public int Id { get; set; }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"ANI File: {Header.NumFrames} Frames, {Header.NumSteps} Steps, " +
               $"Width: {Header.Width}, Height: {Header.Height}, BitCount: {Header.BitCount}";
    }
}
