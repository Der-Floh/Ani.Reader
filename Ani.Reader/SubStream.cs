namespace Ani.Reader;

internal class SubStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _start;
    private readonly long _length;
    private long _position;

    public SubStream(Stream baseStream, long start, long length)
    {
        _baseStream = baseStream;
        _start = start;
        _length = length;
        _position = 0;
        _baseStream.Seek(_start, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= _length)
            return 0;

        count = (int)Math.Min(count, _length - _position);
        var bytesRead = _baseStream.Read(buffer, offset, count);
        _position += bytesRead;
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (newPos < 0 || newPos > _length)
            throw new IOException("Seek operation is out of bounds");

        _position = newPos;
        return _baseStream.Seek(_start + _position, SeekOrigin.Begin) - _start;
    }

    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => _baseStream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
