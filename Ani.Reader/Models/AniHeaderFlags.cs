namespace Ani.Reader.Models;

/// <summary>
/// Represents the header flags of an ANI file, determining how its frames should be interpreted.
/// <para> Reference: <see href="https://www.daubnet.com/en/file-format-ani">ANI File Format Specification</see> </para>
/// </summary>
public class AniHeaderFlags
{
    /// <summary>
    /// The IconFlag determines whether the frames contain icon/cursor data or raw image data.
    /// <para> TRUE: Frames contain icon or cursor data. </para>
    /// <para> FALSE: Frames contain raw image data. </para>
    /// <para>
    /// Only animations whose frames hold icon or cursor data can be read. <see cref="AniReader"/> leaves out animations
    /// with raw frames, so a standalone .ani file of that kind reads as <see langword="null"/>.
    /// </para>
    /// </summary>
    public bool IconFlag { get; set; }

    /// <summary>
    /// The SequenceFlag indicates whether the file contains sequence data.
    /// <para> TRUE: The file contains sequence data. </para>
    /// <para> FALSE: The file does not contain sequence data. </para>
    /// </summary>
    public bool SequenceFlag { get; set; }

    /// <summary>
    /// Should be 0.
    /// </summary>
    public uint Reserved { get; set; }

    /// <summary>
    /// Parses an AniHeaderFlags object from a 32-bit unsigned integer.
    /// </summary>
    /// <param name="flags">A 32-bit value representing the ANI header flags.</param>
    /// <returns>An instance of <see cref="AniHeaderFlags"/> with the extracted flag values.</returns>
    public static AniHeaderFlags FromUInt32(uint flags)
    {
        return new AniHeaderFlags
        {
            IconFlag = (flags & 1) != 0,
            SequenceFlag = (flags & 2) != 0,
            Reserved = flags >> 2
        };
    }

    /// <inheritdoc/>
    public override string ToString()
        => $"IconFlag: {IconFlag}, SequenceFlag: {SequenceFlag}, Reserved: {Reserved}";
}