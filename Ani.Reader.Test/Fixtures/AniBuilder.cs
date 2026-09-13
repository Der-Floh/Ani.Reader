using System.Text;

namespace Ani.Reader.Test.Fixtures;

/// <summary>
/// Writes ANI files in memory, so tests can pair frames with header counts, flags, rate and sequence data and chunk
/// layouts the committed fixture does not carry.
/// </summary>
internal sealed class AniBuilder
{
    public const uint IconFlag = 1;
    public const uint SequenceFlag = 2;

    private const int HeaderSize = 36;

    private readonly List<byte[]> _frames = [];
    private readonly List<long> _frameOffsets = [];
    private readonly List<byte[]> _chunksBeforeHeader = [];
    private readonly List<byte[]> _extraFrameListChunks = [];
    private uint[]? _rates;
    private uint[]? _sequence;
    private uint? _frameCount;
    private uint? _stepCount;
    private uint? _flags;
    private bool _frameListBeforeHeader;

    /// <summary>
    /// The frame payloads of <see cref="TestFiles.AnimatedThreeFrame"/>, read from its chunks directly so the
    /// builder does not depend on the decoder it feeds.
    /// </summary>
    public static IReadOnlyList<byte[]> FixtureFrames { get; } = ReadFrames(TestFiles.BytesOf(TestFiles.AnimatedThreeFrame));

    /// <summary>
    /// A cursor directory declaring no images, which reads as a frame that holds no image.
    /// </summary>
    public static byte[] EmptyCursor => [0, 0, 2, 0, 0, 0];

    /// <summary>
    /// The display rate written to the header, in jiffies.
    /// </summary>
    public uint DisplayRate { get; set; } = 10;

    /// <summary>
    /// Where each frame payload starts in the bytes returned by the last <see cref="Build"/>.
    /// </summary>
    public IReadOnlyList<long> FrameOffsets => _frameOffsets;

    public static AniBuilder FromFixture()
    {
        var builder = new AniBuilder();
        foreach (var frame in FixtureFrames)
            builder.AddFrame(frame);

        return builder;
    }

    public AniBuilder AddFrame(byte[] payload)
    {
        _frames.Add(payload);
        return this;
    }

    public AniBuilder WithRates(params uint[] rates)
    {
        _rates = rates;
        return this;
    }

    public AniBuilder WithSequence(params uint[] sequence)
    {
        _sequence = sequence;
        return this;
    }

    /// <summary>Writes this frame count into the header instead of the number of frames added.</summary>
    public AniBuilder WithFrameCount(uint frameCount)
    {
        _frameCount = frameCount;
        return this;
    }

    /// <summary>Writes this step count into the header instead of the sequence length or the frame count.</summary>
    public AniBuilder WithStepCount(uint stepCount)
    {
        _stepCount = stepCount;
        return this;
    }

    /// <summary>Writes these flags into the header instead of the icon flag and, with a sequence, the sequence flag.</summary>
    public AniBuilder WithFlags(uint flags)
    {
        _flags = flags;
        return this;
    }

    /// <summary>Writes a complete chunk, such as one made by <see cref="Chunk"/>, between the ACON marker and the header.</summary>
    public AniBuilder WithChunkBeforeHeader(byte[] chunk)
    {
        _chunksBeforeHeader.Add(chunk);
        return this;
    }

    /// <summary>Writes a complete chunk into the frame list after the frames.</summary>
    public AniBuilder WithFrameListChunk(byte[] chunk)
    {
        _extraFrameListChunks.Add(chunk);
        return this;
    }

    /// <summary>Writes the frame list before the header rather than after it.</summary>
    public AniBuilder WithFrameListBeforeHeader()
    {
        _frameListBeforeHeader = true;
        return this;
    }

    /// <summary>
    /// A chunk with the given id and data. RIFF pads odd sized data with one byte, which <paramref name="pad"/> can leave
    /// out to imitate writers that forget it.
    /// </summary>
    public static byte[] Chunk(string id, byte[] data, bool pad = true)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        WriteChunkHeader(writer, id, data.Length);
        writer.Write(data);
        if (pad && data.Length % 2 != 0)
            writer.Write((byte)0);

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>A <c>LIST INFO</c> chunk holding the given sub-chunks.</summary>
    public static byte[] InfoList(params byte[][] subChunks)
        => Chunk("LIST", [.. Encoding.ASCII.GetBytes("INFO"), .. subChunks.SelectMany(chunk => chunk)]);

    public byte[] Build()
    {
        _frameOffsets.Clear();

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(0u);
        writer.Write(Encoding.ASCII.GetBytes("ACON"));

        foreach (var chunk in _chunksBeforeHeader)
            writer.Write(chunk);

        if (_frameListBeforeHeader)
            WriteFrameList(writer);

        WriteChunkHeader(writer, "anih", HeaderSize);
        writer.Write((uint)HeaderSize);
        writer.Write(_frameCount ?? (uint)_frames.Count);
        writer.Write(_stepCount ?? (uint)(_sequence?.Length ?? _frames.Count));
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(DisplayRate);
        writer.Write(_flags ?? (IconFlag | (_sequence is null ? 0 : SequenceFlag)));

        if (_rates is not null)
            WriteValues(writer, "rate", _rates);

        if (_sequence is not null)
            WriteValues(writer, "seq ", _sequence);

        if (!_frameListBeforeHeader)
            WriteFrameList(writer);

        writer.Flush();
        var bytes = stream.ToArray();
        BitConverter.GetBytes((uint)(bytes.Length - 8)).CopyTo(bytes, 4);

        return bytes;
    }

    private void WriteFrameList(BinaryWriter writer)
    {
        var frameListSize = 4 + _frames.Sum(frame => 8 + Padded(frame.Length)) + _extraFrameListChunks.Sum(chunk => chunk.Length);

        WriteChunkHeader(writer, "LIST", frameListSize);
        writer.Write(Encoding.ASCII.GetBytes("fram"));
        foreach (var frame in _frames)
        {
            WriteChunkHeader(writer, "icon", frame.Length);
            _frameOffsets.Add(writer.BaseStream.Position);
            writer.Write(frame);
            if (frame.Length % 2 != 0)
                writer.Write((byte)0);
        }

        foreach (var chunk in _extraFrameListChunks)
            writer.Write(chunk);
    }

    private static void WriteChunkHeader(BinaryWriter writer, string id, int size)
    {
        writer.Write(Encoding.ASCII.GetBytes(id));
        writer.Write((uint)size);
    }

    private static void WriteValues(BinaryWriter writer, string id, uint[] values)
    {
        WriteChunkHeader(writer, id, 4 * values.Length);
        foreach (var value in values)
            writer.Write(value);
    }

    private static int Padded(int length) => length + (length % 2);

    private static List<byte[]> ReadFrames(byte[] ani)
    {
        var frames = new List<byte[]>();
        var offset = 12;
        while (offset + 8 <= ani.Length)
        {
            var id = Encoding.ASCII.GetString(ani, offset, 4);
            var size = (int)BitConverter.ToUInt32(ani, offset + 4);
            var payload = offset + 8;

            if (id == "LIST" && Encoding.ASCII.GetString(ani, payload, 4) == "fram")
            {
                var subOffset = payload + 4;
                while (subOffset + 8 <= payload + size)
                {
                    var subSize = (int)BitConverter.ToUInt32(ani, subOffset + 4);
                    frames.Add(ani.AsSpan(subOffset + 8, subSize).ToArray());
                    subOffset += 8 + Padded(subSize);
                }
            }

            offset = payload + Padded(size);
        }

        return frames;
    }
}
