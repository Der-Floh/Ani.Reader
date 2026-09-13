namespace Ani.Reader.Models;

/// <summary>
/// Represents raw parsed ANI animation data extracted from a RIFF stream.
/// </summary>
public sealed class AniEntry
{
    /// <summary>
    /// Text from the RIFF <c>LIST INFO</c> chunk, keyed by sub-chunk identifier, such as <c>INAM</c> for the title
    /// and <c>IART</c> for the author.
    /// <para> Empty when the file carries no <c>INFO</c> list. </para>
    /// <para>
    /// An identifier that repeats keeps its first value, and reading stops at an entry that runs past the end of the
    /// list. Neither affects the rest of the animation.
    /// </para>
    /// </summary>
    public Dictionary<string, string> MetaData { get; set; } = [];

    /// <summary>
    /// The <c>anih</c> chunk, describing how many frames and steps the animation has and how its frames are stored.
    /// </summary>
    public AniHeader Header { get; set; } = null!;

    /// <summary>
    /// The display rate of each step from the <c>rate</c> chunk, in 1/60th of a second (jiffies).
    /// <para> Holds one entry per step, in step order, parallel to <see cref="FrameSequence"/>. </para>
    /// <para> Empty when the file has no <c>rate</c> chunk, in which case every step uses <see cref="AniHeader.DisplayRate"/>. </para>
    /// </summary>
    public List<uint> FrameRates { get; set; } = [];

    /// <summary>
    /// The frame shown at each step from the <c>seq </c> chunk, as an index into <see cref="Frames"/>.
    /// <para> Holds one entry per step, so the same frame can be shown more than once. </para>
    /// <para> Empty when the file has no <c>seq </c> chunk, in which case each frame is one step, in stored order. </para>
    /// </summary>
    public List<uint> FrameSequence { get; set; } = [];

    /// <summary>
    /// References to the frame images, one per <c>icon</c> chunk in the <c>LIST fram</c> chunk, in the order they are
    /// stored.
    /// <para> Other chunks inside the list are skipped. </para>
    /// </summary>
    public List<AniFrameReference> Frames { get; set; } = [];

    /// <summary>
    /// The offset of this animation's RIFF data within the stream it was read from.
    /// <para>
    /// Zero for an animation read from the start of a stream, such as a standalone .ani file. For an animated cursor
    /// resource inside an executable or DLL it is the resource's file offset.
    /// </para>
    /// </summary>
    public long EntryOffset { get; set; }

    /// <summary>
    /// The size in bytes of this animation's RIFF data when it was read from part of a stream, such as an animated
    /// cursor resource inside an executable or DLL.
    /// <para> Zero for an animation read from a whole stream, such as a standalone .ani file. </para>
    /// </summary>
    public long EntrySize { get; set; }

    /// <summary>
    /// The resource identifier of this animation within an executable or DLL.
    /// <para> Zero for a standalone .ani file, and for a resource identified by name rather than by number. </para>
    /// </summary>
    public int Id { get; set; }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"ANI File: {Header.NumFrames} Frames, {Header.NumSteps} Steps, " +
               $"Width: {Header.Width}, Height: {Header.Height}, BitCount: {Header.BitCount}";
    }
}
