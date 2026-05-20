namespace Ani.Reader.Models;

/// <summary>
/// Represents timing and positional data for an animation frame.
/// </summary>
public class FrameInformation
{
    /// <summary>
    /// The position of the frame in the animation sequence.
    /// <para> Starts from 0 and increments sequentially. </para>
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// The time at which this frame starts playing in the animation.
    /// </summary>
    public TimeSpan Start { get; set; }

    /// <summary>
    /// The duration for which this frame is displayed before advancing to the next frame.
    /// </summary>
    public TimeSpan Duration { get; set; }

    public IEnumerable<FrameVariationInformation> VariationDetails { get; set; } = Enumerable.Empty<FrameVariationInformation>();

    /// <summary>
    /// A reference to the frame's data within the ANI file.
    /// <para> Provides access to frame offset and size. </para>
    /// </summary>
    public AniFrameReference FrameReference { get; set; } = null!;
}
