namespace Ani.Reader.Models;

/// <summary>
/// The hotspot of one frame for an <see cref="AnimationInformation"/>, as listed by
/// <see cref="AnimationInformation.FrameHotspots"/>.
/// </summary>
public class FrameHotspot : HotspotInformation
{
    /// <summary>
    /// The frame this hotspot belongs to, matching <see cref="FrameInformation.Position"/>.
    /// </summary>
    public int FramePosition { get; set; }
}

/// <summary>
/// A cursor hotspot: the pixel within the image that sits at the pointer's position.
/// <para> Both coordinates are 0 for icon images, which carry no hotspot. </para>
/// </summary>
public class HotspotInformation
{
    /// <summary>
    /// The horizontal position of the hotspot in pixels, counted from the left edge of the image.
    /// </summary>
    public ushort HotspotX { get; set; }

    /// <summary>
    /// The vertical position of the hotspot in pixels, counted from the top edge of the image.
    /// </summary>
    public ushort HotspotY { get; set; }
}

/// <summary>
/// One image stored in a single frame. A frame's icon or cursor data can hold the same picture at several sizes and
/// color depths, each described by one of these.
/// </summary>
public class FrameVariationInformation : HotspotInformation
{
    /// <summary>
    /// The width of the image in pixels.
    /// </summary>
    public int Width { get; internal set; }

    /// <summary>
    /// The height of the image in pixels.
    /// </summary>
    public int Height { get; internal set; }

    /// <summary>
    /// The number of bits per pixel the image is stored with.
    /// </summary>
    public int BitCount { get; internal set; }
}

/// <summary>
/// One size the whole animation is available in: every frame that holds an image has one of this size, so the animation
/// can be played back at it. When the frames share no size, <see cref="AniData.Animations"/> lists the sizes of the
/// first frame with an image, and the other frames give the image closest in size.
/// <para>
/// Pass it to <see cref="AniData.GetFrameBytes"/>, <see cref="AniData.SaveImages"/> or <see cref="WebPCreator"/> to take
/// that image from each frame. <see cref="AniData.PreferredAnimationIndex"/> picks the best of them.
/// </para>
/// </summary>
public class AnimationInformation
{
    /// <summary>
    /// The width of the image in pixels.
    /// </summary>
    public int Width { get; internal set; }

    /// <summary>
    /// The height of the image in pixels.
    /// </summary>
    public int Height { get; internal set; }

    /// <summary>
    /// The number of bits per pixel of this size, counting a PNG compressed image as 32-bit.
    /// <para>
    /// Reported by the first frame that contains this variant. A frame may store the same variant at a different
    /// depth, so this value does not take part in equality.
    /// </para>
    /// </summary>
    public int BitCount { get; internal set; }

    /// <summary>
    /// The hotspot of this size's image in every frame, one entry per entry in <see cref="AniData.Frames"/>.
    /// <para>
    /// Match an entry to its frame through <see cref="FrameHotspot.FramePosition"/>. Frames can differ in their
    /// hotspot, so use each frame's own rather than assuming the first applies throughout.
    /// </para>
    /// </summary>
    public IEnumerable<FrameHotspot> FrameHotspots { get; internal set; } = Enumerable.Empty<FrameHotspot>();

    /// <summary>
    /// Two variants are the same when they describe the same size. Bit depth is a per-frame storage detail:
    /// an encoder may pick a smaller palette for frames that need fewer colors, and those frames still belong
    /// to the same animation variant.
    /// </summary>
    public static bool operator ==(AnimationInformation left, AnimationInformation right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        return left.Width == right.Width &&
               left.Height == right.Height;
    }

    /// <summary>
    /// Two variants differ when they describe different sizes. See
    /// <see cref="operator ==(AnimationInformation, AnimationInformation)"/>.
    /// </summary>
    public static bool operator !=(AnimationInformation left, AnimationInformation right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Reports whether <paramref name="obj"/> is an <see cref="AnimationInformation"/> of the same size. Bit depth does
    /// not take part.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true"/> if both describe the same width and height.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is not AnimationInformation other)
            return false;

        return this == other;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(Width, Height);

    /// <inheritdoc/>
    public override string ToString()
        => $"{Width}x{Height}, BitCount: {BitCount}";
}