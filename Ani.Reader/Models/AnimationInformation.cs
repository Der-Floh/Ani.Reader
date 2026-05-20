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

    public static bool operator ==(AnimationInformation left, AnimationInformation right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        return left.Width == right.Width &&
               left.Height == right.Height &&
               left.BitCount == right.BitCount;
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
        => HashCode.Combine(Width, Height, BitCount);
}