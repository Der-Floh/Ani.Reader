namespace Ani.Reader.Models;

public class FrameHotspot : HotspotInformation
{
    public int FramePosition { get; set; }
}

public class HotspotInformation
{
    public ushort HotspotX { get; set; }
    public ushort HotspotY { get; set; }
}

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
    /// The bit depth of the image, indicating the number of bits used for each color component.
    /// </summary>
    public int BitCount { get; internal set; }
}

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
    /// The bit depth of the image, indicating the number of bits used for each color component.
    /// <para>
    /// Reported by the first frame that contains this variant. A frame may store the same variant at a different
    /// depth, so this value does not take part in equality.
    /// </para>
    /// </summary>
    public int BitCount { get; internal set; }

    /// <summary>
    /// The X-coordinate of the cursor's hotspot.
    /// This defines the exact point within the cursor image that interacts with the user interface.
    /// <para>
    /// This property is only relevant for cursor (CUR) images.
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

    public static bool operator !=(AnimationInformation left, AnimationInformation right)
    {
        return !(left == right);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not AnimationInformation other)
            return false;

        return this == other;
    }

    public override int GetHashCode()
        => HashCode.Combine(Width, Height);

    public override string ToString()
        => $"{Width}x{Height}, BitCount: {BitCount}";
}