namespace Ani.Reader.Models;
/// <summary>
/// Represents a reference to a specific frame within an ANI file.
/// </summary>
public class AniFrameReference
{
    /// <summary>
    /// The absolute offset of the frame within the source stream.
    /// <para> This value is used to correctly locate the frame's data in the file. </para>
    /// </summary>
    public long RealOffset { get; set; }

    /// <summary>
    /// The offset of the frame's data within the animation's RIFF data.
    /// <para>
    /// Equal to <see cref="RealOffset"/> for a standalone .ani file. For an animation stored in an executable or DLL it
    /// is counted from the start of the resource instead.
    /// </para>
    /// </summary>
    public long Offset { get; set; }

    /// <summary>
    /// The size of the frame data in bytes.
    /// <para> Rounded up to an even number, since RIFF pads every chunk to a 2-byte boundary. </para>
    /// </summary>
    public uint Size { get; set; }

    /// <summary>
    /// Retrieves a stream containing the frame data from the source stream.
    /// </summary>
    /// <param name="sourceStream">The input stream containing ANI file data.</param>
    /// <returns>A <see cref="Stream"/> containing only the frame's data.</returns>
    public Stream GetFrameStream(Stream sourceStream)
    {
        sourceStream.Seek(RealOffset, SeekOrigin.Begin);
        return new SubStream(sourceStream, RealOffset, Size);
    }

    /// <inheritdoc/>
    public override string ToString()
        => $"Frame at {Offset}, Size: {Size}";
}
