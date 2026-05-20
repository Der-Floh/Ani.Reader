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
    /// The relative offset of the frame within the frame list.
    /// <para> This offset may differ from <see cref="RealOffset"/> based on how frames are organized. </para>
    /// </summary>
    public long Offset { get; set; }

    /// <summary>
    /// The size of the frame data in bytes.
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

    public override string ToString()
        => $"Frame at {Offset}, Size: {Size}";
}
