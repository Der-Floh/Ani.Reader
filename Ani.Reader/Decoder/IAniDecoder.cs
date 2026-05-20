using Ani.Reader.Models;

namespace Ani.Reader.Decoder;

/// <summary>
/// Defines functionality for decoding ANI animation data from streams.
/// </summary>
public interface IAniDecoder
{
    /// <summary>
    /// Reads and decodes ANI data from the provided stream.
    /// </summary>
    /// <param name="stream">The stream containing ANI RIFF data.</param>
    /// <returns>An <see cref="AniEntry"/> containing the decoded ANI data, or <see langword="null"/> if decoding fails.</returns>
    AniEntry? Read(Stream stream);

    /// <summary>
    /// Reads and decodes ANI data from a specific offset within the provided stream.
    /// </summary>
    /// <param name="stream">The stream containing ANI RIFF data.</param>
    /// <param name="offset">The byte offset within the stream to start reading from.</param>
    /// <param name="size">The number of bytes to read.</param>
    /// <returns>An <see cref="AniEntry"/> containing the decoded ANI data, or <see langword="null"/> if decoding fails.</returns>
    AniEntry? Read(Stream stream, long offset, long size);
}
