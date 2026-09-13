namespace Ani.Reader.Models;

/// <summary>
/// The <c>anih</c> chunk every ANI file starts with, describing how many frames and steps the animation holds, how
/// long each step is shown by default, and how the frames are stored.
/// </summary>
public class AniHeader
{
    /// <summary>
    /// The size of this structure in bytes.
    /// <para> Expected value: 36 bytes, the nine 4-byte fields of the chunk. </para>
    /// </summary>
    public uint HeaderSize { get; set; }

    /// <summary>
    /// The number of frames this animation declares.
    /// <para> Each frame can be an icon, cursor, or raw image. Windows uses only this many of the stored frames. </para>
    /// </summary>
    public uint NumFrames { get; set; }

    /// <summary>
    /// The number of steps in the animation sequence.
    /// <para> Several steps can show the same frame. </para>
    /// <para> Without a <c>seq </c> chunk, the steps show the first <see cref="NumSteps"/> frames in order. </para>
    /// </summary>
    public uint NumSteps { get; set; }

    /// <summary>
    /// The width of each frame in pixels when the frames are raw bitmaps.
    /// <para>
    /// Icon and cursor frames describe their own sizes, and files built from them commonly leave this 0, as the
    /// cursors shipped with Windows do. Use <see cref="AniData.Animations"/> for the sizes such an animation contains.
    /// </para>
    /// </summary>
    public uint Width { get; set; }

    /// <summary>
    /// The height of each frame in pixels when the frames are raw bitmaps.
    /// <para> Commonly 0 for icon and cursor frames; see <see cref="Width"/>. </para>
    /// </summary>
    public uint Height { get; set; }

    /// <summary>
    /// The number of bits per pixel of each frame when the frames are raw bitmaps.
    /// <para> Icon and cursor frames carry their own color depth. </para>
    /// </summary>
    public uint BitCount { get; set; }

    /// <summary>
    /// The number of color planes when the frames are raw bitmaps, which is 1.
    /// <para> Files built from icon or cursor frames may leave this 0. </para>
    /// </summary>
    public uint NumPlanes { get; set; }

    /// <summary>
    /// The default display rate of the animation.
    /// <para> Defined in units of 1/60th of a second (Jiffies). </para>
    /// <para> Frame Rate (fps) = 60 / DisplayRate </para>
    /// <para> Applies to every step when the file has no <c>rate</c> chunk. </para>
    /// </summary>
    public uint DisplayRate { get; set; }

    /// <summary>
    /// The header flags that define how the frames should be interpreted.
    /// <para> Only the first two bits are used, the remaining bits are reserved. </para>
    /// </summary>
    public AniHeaderFlags Flags { get; set; } = null!;

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"HeaderSize: {HeaderSize}, NumFrames: {NumFrames}, NumSteps: {NumSteps}, " +
               $"Width: {Width}, Height: {Height}, BitCount: {BitCount}, NumPlanes: {NumPlanes}, " +
               $"DisplayRate: {DisplayRate}, Flags: {Flags}";
    }
}