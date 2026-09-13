using Ani.Reader.Models;

namespace Ani.Reader.Decoder;

/// <summary>
/// Defines functionality for decoding ANI data from PE files (executables and DLLs).
/// </summary>
public interface IAniPeDecoder
{
    /// <summary>
    /// Decodes ANI cursor resources from the given PE file stream.
    /// </summary>
    /// <param name="stream">The stream containing PE file data that may include ANI cursor resources.</param>
    /// <returns>
    /// A <see cref="DecodedAniResult"/> containing the decoded ANI entries and metadata, or <see langword="null"/> if the
    /// stream does not hold an image. An image without animated cursors gives a result without entries.
    /// </returns>
    /// <exception cref="InvalidDataException">The headers or the resource tree of the image are malformed.</exception>
    /// <exception cref="EndOfStreamException">The stream ends inside the headers or the resource tree.</exception>
    DecodedAniResult? GetDecodedAniResult(Stream stream);

    /// <summary>
    /// Checks whether the given stream represents a PE file.
    /// </summary>
    /// <param name="stream">The stream to check.</param>
    /// <returns><see langword="true"/> if the stream represents a PE file; otherwise, <see langword="false"/>.</returns>
    bool IsPeFormat(Stream stream);
}
