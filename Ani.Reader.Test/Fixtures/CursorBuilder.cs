namespace Ani.Reader.Test.Fixtures;

/// <summary>
/// Writes cursor files whose images are filled with one colour, so a test can tell frames apart by colour and give
/// them whichever sizes it needs.
/// </summary>
internal static class CursorBuilder
{
    private const int DirectoryHeaderSize = 6;
    private const int DirectoryEntrySize = 16;
    private const int InfoHeaderSize = 40;

    /// <summary>
    /// A cursor file holding one opaque 32-bit image per size, every one of them in the given colour.
    /// </summary>
    public static byte[] Solid((byte Red, byte Green, byte Blue) colour, params int[] sizes)
    {
        var images = sizes.Select(size => Image(size, colour)).ToArray();

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);
        writer.Write((ushort)2);
        writer.Write((ushort)sizes.Length);

        var offset = DirectoryHeaderSize + (DirectoryEntrySize * sizes.Length);
        for (var i = 0; i < sizes.Length; i++)
        {
            writer.Write((byte)(sizes[i] % 256));
            writer.Write((byte)(sizes[i] % 256));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)(sizes[i] / 2));
            writer.Write((ushort)(sizes[i] / 2));
            writer.Write((uint)images[i].Length);
            writer.Write((uint)offset);
            offset += images[i].Length;
        }

        foreach (var image in images)
            writer.Write(image);

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] Image(int size, (byte Red, byte Green, byte Blue) colour)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(InfoHeaderSize);
        writer.Write(size);
        writer.Write(size * 2);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);
        writer.Write(size * size * 4);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        for (var pixel = 0; pixel < size * size; pixel++)
        {
            writer.Write(colour.Blue);
            writer.Write(colour.Green);
            writer.Write(colour.Red);
            writer.Write((byte)255);
        }

        writer.Write(new byte[(size + 31) / 32 * 4 * size]);
        writer.Flush();

        return stream.ToArray();
    }
}
