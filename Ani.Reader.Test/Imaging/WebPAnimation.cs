using System.Text;

using ImageMagick;

namespace Ani.Reader.Test.Imaging;

internal sealed record WebPFrameInfo(int DurationMilliseconds, bool BlendWithCanvas, bool DisposeToBackground);

/// <summary>
/// Reads an animated WebP the way a player renders it.
/// <para>
/// MagickImageCollection.Coalesce cannot be used for this: it applies GIF semantics and always alpha-blends
/// a frame onto the canvas, ignoring the per-frame blend flag that WebP stores in its ANMF chunks. Frames
/// written with "no blend" replace the canvas, transparency included, and coalescing them hides that.
/// </para>
/// </summary>
internal static class WebPAnimation
{
    /// <summary>
    /// Reads the per-frame blend and dispose flags straight out of the WebP container.
    /// <para>Format reference: https://developers.google.com/speed/webp/docs/riff_container</para>
    /// </summary>
    public static IReadOnlyList<WebPFrameInfo> ReadFrameInfo(byte[] webp)
    {
        if (webp.Length < 12 || Ascii(webp, 0, 4) != "RIFF" || Ascii(webp, 8, 4) != "WEBP")
            throw new InvalidDataException("Not a WebP file.");

        var frames = new List<WebPFrameInfo>();
        var offset = 12;
        while (offset + 8 <= webp.Length)
        {
            var chunkId = Ascii(webp, offset, 4);
            var chunkSize = (int)BitConverter.ToUInt32(webp, offset + 4);
            var payload = offset + 8;

            if (chunkId == "ANMF")
            {
                var duration = webp[payload + 12] | (webp[payload + 13] << 8) | (webp[payload + 14] << 16);
                var flags = webp[payload + 15];
                frames.Add(new WebPFrameInfo(
                    DurationMilliseconds: duration,
                    BlendWithCanvas: (flags & 0x02) == 0,
                    DisposeToBackground: (flags & 0x01) != 0));
            }

            offset = payload + chunkSize + (chunkSize & 1);
        }

        return frames;
    }

    /// <summary>
    /// Returns the fully composited frames, i.e. what each step of the animation actually looks like.
    /// The caller owns the returned images.
    /// </summary>
    public static List<MagickImage> Composite(byte[] webp)
    {
        var info = ReadFrameInfo(webp);
        using var stored = new MagickImageCollection(webp);

        if (info.Count != stored.Count)
            throw new InvalidDataException($"Found {info.Count} ANMF chunks but {stored.Count} decoded frames.");

        var width = stored.Max(f => f.Page.X + (int)f.Width);
        var height = stored.Max(f => f.Page.Y + (int)f.Height);

        var composited = new List<MagickImage>();
        using var canvas = new MagickImage(MagickColors.Transparent, (uint)width, (uint)height);

        for (var i = 0; i < stored.Count; i++)
        {
            var frame = stored[i];
            var operation = info[i].BlendWithCanvas ? CompositeOperator.Over : CompositeOperator.Copy;
            canvas.Composite(frame, frame.Page.X, frame.Page.Y, operation);

            composited.Add(new MagickImage(canvas));

            if (info[i].DisposeToBackground)
            {
                using var hole = new MagickImage(MagickColors.Transparent, frame.Width, frame.Height);
                canvas.Composite(hole, frame.Page.X, frame.Page.Y, CompositeOperator.Copy);
            }
        }

        return composited;
    }

    private static string Ascii(byte[] data, int offset, int length) =>
        Encoding.ASCII.GetString(data, offset, length);
}
