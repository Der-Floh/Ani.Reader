namespace Ani.Reader.Models;

public class AniHeader
{
    /// <summary>
    /// The size of this structure in bytes.
    /// <para> Expected value: 32 bytes. </para>
    /// </summary>
    public uint HeaderSize { get; set; }

    /// <summary>
    /// The number of frames stored in this animation.
    /// <para> Each frame can be an icon, cursor, or raw image. </para>
    /// </summary>
    public uint NumFrames { get; set; }

    /// <summary>
    /// The number of steps in the animation sequence.
    /// <para> May include duplicate frames. </para>
    /// <para> Equals <see cref="NumFrames"/> if no 'seq ' chunk is present. </para>
    /// </summary>
    public uint NumSteps { get; set; }

    /// <summary>
    /// The total width of the animation in pixels.
    /// </summary>
    public uint Width { get; set; }

    /// <summary>
    /// The total height of the animation in pixels.
    /// </summary>
    public uint Height { get; set; }

    /// <summary>
    /// The number of bits per pixel, defining the color depth.
    /// </summary>
    public uint BitCount { get; set; }

    /// <summary>
    /// The number of color planes. Always set to 1.
    /// </summary>
    public uint NumPlanes { get; set; }

    /// <summary>
    /// The default display rate of the animation.
    /// <para> Defined in units of 1/60th of a second (Jiffies). </para>
    /// <para> Frame Rate (fps) = 60 / DisplayRate </para>
    /// </summary>
    public uint DisplayRate { get; set; }

    /// <summary>
    /// The header flags that define how the frames should be interpreted.
    /// <para> Only the first two bits are used, the remaining bits are reserved. </para>
    /// </summary>
    public AniHeaderFlags Flags { get; set; } = null!;

    public override string ToString()
    {
        return $"HeaderSize: {HeaderSize}, NumFrames: {NumFrames}, NumSteps: {NumSteps}, " +
               $"Width: {Width}, Height: {Height}, BitCount: {BitCount}, NumPlanes: {NumPlanes}, " +
               $"DisplayRate: {DisplayRate}, Flags: {Flags}";
    }
}