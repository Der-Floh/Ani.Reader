using Ico.Reader.Data.Source;

namespace Ani.Reader;

/// <summary>
/// Exposes a caller-owned stream as an <see cref="IDataSource"/>. Every returned stream is a non-owning
/// window over the original, so the decoding pipeline can dispose it without closing the caller's stream.
/// </summary>
internal sealed class SharedStreamSource : IDataSource
{
    private readonly Stream _stream;

    public SharedStreamSource(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public Stream GetStream(bool useAsync = false) => new SubStream(_stream, 0, _stream.Length);
}
