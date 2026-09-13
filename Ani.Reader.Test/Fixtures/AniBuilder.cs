using System.Text;

namespace Ani.Reader.Test.Fixtures;

/// <summary>
/// Writes ANI files in memory, so tests can pair the frames of <see cref="TestFiles.AnimatedThreeFrame"/> with
/// rate and sequence data the committed fixture does not carry.
/// </summary>
internal sealed class AniBuilder
{
    private const int HeaderSize = 36;
    private const uint IconFlag = 1;
    private const uint SequenceFlag = 2;

    private readonly List<byte[]> _frames = [];
    private readonly List<long> _frameOffsets = [];
    private uint[]? _rates;
    private uint[]? _sequence;

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

    public byte[] Build()
    {
        _frameOffsets.Clear();

        var frameListSize = 4 + _frames.Sum(frame => 8 + Padded(frame.Length));
        var riffSize = 4
            + 8 + HeaderSize
            + (_rates is null ? 0 : 8 + (4 * _rates.Length))
            + (_sequence is null ? 0 : 8 + (4 * _sequence.Length))
            + 8 + frameListSize;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        WriteChunkHeader(writer, "RIFF", riffSize);
        writer.Write(Encoding.ASCII.GetBytes("ACON"));

        WriteChunkHeader(writer, "anih", HeaderSize);
        writer.Write((uint)HeaderSize);
        writer.Write((uint)_frames.Count);
        writer.Write((uint)(_sequence?.Length ?? _frames.Count));
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(DisplayRate);
        writer.Write(IconFlag | (_sequence is null ? 0 : SequenceFlag));

        if (_rates is not null)
            WriteValues(writer, "rate", _rates);

        if (_sequence is not null)
            WriteValues(writer, "seq ", _sequence);

        WriteChunkHeader(writer, "LIST", frameListSize);
        writer.Write(Encoding.ASCII.GetBytes("fram"));
        foreach (var frame in _frames)
        {
            WriteChunkHeader(writer, "icon", frame.Length);
            _frameOffsets.Add(stream.Position);
            writer.Write(frame);
            if (frame.Length % 2 != 0)
                writer.Write((byte)0);
        }

        writer.Flush();
        return stream.ToArray();
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
