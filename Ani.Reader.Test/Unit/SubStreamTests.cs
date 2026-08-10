using Xunit;

namespace Ani.Reader.Test.Unit;

public sealed class SubStreamTests
{
    private static readonly byte[] Source = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

    private static SubStream Window(long start, long length) => new(new MemoryStream(Source), start, length);

    [Fact]
    public void Read_ReturnsOnlyTheWindow()
    {
        using var stream = Window(3, 4);
        var buffer = new byte[10];

        var read = stream.Read(buffer, 0, buffer.Length);

        Assert.Equal(4, read);
        Assert.Equal<byte[]>([3, 4, 5, 6], buffer[..read]);
    }

    [Fact]
    public void Read_PastTheEnd_ReturnsZero()
    {
        using var stream = Window(3, 4);
        var buffer = new byte[10];

        var first = stream.Read(buffer, 0, buffer.Length);

        Assert.Equal(4, first);
        Assert.Equal(0, stream.Read(buffer, 0, buffer.Length));
    }

    [Fact]
    public void Length_IsTheWindowLength()
    {
        using var stream = Window(3, 4);

        Assert.Equal(4L, stream.Length);
        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);
    }

    [Theory]
    [InlineData(SeekOrigin.Begin, 2L, 2L)]
    [InlineData(SeekOrigin.End, -1L, 3L)]
    public void Seek_MovesRelativeToTheWindow(SeekOrigin origin, long offset, long expectedPosition)
    {
        using var stream = Window(3, 4);

        var position = stream.Seek(offset, origin);

        Assert.Equal(expectedPosition, position);
        Assert.Equal(expectedPosition, stream.Position);
        Assert.Equal(Source[3 + expectedPosition], (byte)stream.ReadByte());
    }

    [Fact]
    public void Seek_FromCurrent_IsRelativeToThePosition()
    {
        using var stream = Window(3, 4);
        stream.Seek(1, SeekOrigin.Begin);

        Assert.Equal(3L, stream.Seek(2, SeekOrigin.Current));
    }

    [Fact]
    public void Position_Setter_SeeksWithinTheWindow()
    {
        using var stream = Window(3, 4);

        stream.Position = 1;

        Assert.Equal(1L, stream.Position);
        Assert.Equal(Source[4], (byte)stream.ReadByte());
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(5L)]
    public void Seek_OutsideTheWindow_Throws(long offset)
    {
        using var stream = Window(3, 4);

        Assert.Throws<IOException>(() => stream.Seek(offset, SeekOrigin.Begin));
    }

    [Fact]
    public void Write_IsNotSupported()
    {
        using var stream = Window(3, 4);

        Assert.False(stream.CanWrite);
        Assert.Throws<NotSupportedException>(() => stream.Write([1], 0, 1));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(1));
    }
}
