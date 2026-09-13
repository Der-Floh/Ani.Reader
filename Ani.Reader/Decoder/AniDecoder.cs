using System.Text;

using Ani.Reader.Models;

namespace Ani.Reader.Decoder;

/// <summary>
/// Provides functionality for decoding ANI animation data from RIFF streams.
/// </summary>
/// <remarks>
/// <para>
/// Like Windows, the decoder walks the chunks to the end of the data whatever size the RIFF header states, and skips
/// the pad byte that follows a chunk of odd size.
/// </para>
/// <para>
/// Reading returns <see langword="null"/> when the data is not a RIFF <c>ACON</c> form, holds no <c>anih</c> chunk
/// before its frame list, or holds no frames.
/// </para>
/// </remarks>
public sealed class AniDecoder : IAniDecoder
{
    private const int ChunkHeaderSize = 8;
    private const int IdSize = 4;
    private const int ValueSize = 4;
    private const int HeaderFieldCount = 9;

    /// <inheritdoc/>
    public AniEntry? Read(Stream stream)
    {
        try
        {
            return ParseFromStream(stream);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public AniEntry? Read(Stream stream, long offset, long size)
    {
        try
        {
            using var subStream = new SubStream(stream, offset, size);
            var entry = ParseFromStream(subStream);
            if (entry is null)
                return null;

            entry.EntryOffset = offset;
            entry.EntrySize = size;
            foreach (var frame in entry.Frames)
                frame.RealOffset = offset + frame.Offset;

            return entry;
        }
        catch
        {
            return null;
        }
    }

    private static AniEntry? ParseFromStream(Stream stream)
    {
        var reader = new BinaryReader(stream, Encoding.ASCII);
        var dataEnd = stream.Length;

        if (dataEnd - stream.Position < ChunkHeaderSize + IdSize || ReadId(reader) != "RIFF")
            return null;

        reader.ReadUInt32();
        if (ReadId(reader) != "ACON")
            return null;

        var entry = new AniEntry();
        while (dataEnd - stream.Position >= ChunkHeaderSize)
        {
            var chunkId = ReadId(reader);
            var chunkSize = reader.ReadUInt32();
            var chunkStart = stream.Position;
            var chunkEnd = Math.Min(chunkStart + chunkSize, dataEnd);

            switch (chunkId)
            {
                case "anih":
                    entry.Header = ReadHeader(reader, chunkEnd);
                    break;
                case "rate":
                    entry.FrameRates = ReadValues(reader, chunkEnd);
                    break;
                case "seq ":
                    entry.FrameSequence = ReadValues(reader, chunkEnd);
                    break;
                case "LIST" when chunkEnd - chunkStart >= IdSize:
                    var listType = ReadId(reader);
                    if (listType == "fram")
                    {
                        if (entry.Header is null)
                            return null;

                        entry.Frames.AddRange(ReadFrameReferences(reader, chunkEnd));
                    }
                    else if (listType == "INFO")
                    {
                        ReadInfo(reader, entry.MetaData, chunkEnd);
                    }
                    break;
            }

            var nextChunk = chunkStart + Padded(chunkSize);
            if (nextChunk > dataEnd)
                break;

            stream.Position = nextChunk;
        }

        return entry.Header is null || entry.Frames.Count == 0 ? null : entry;
    }

    private static string ReadId(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(IdSize));

    private static long Padded(uint size) => size + (size & 1L);

    private static bool HasValue(BinaryReader reader, long end) => end - reader.BaseStream.Position >= ValueSize;

    private static AniHeader ReadHeader(BinaryReader reader, long chunkEnd)
    {
        var fields = new uint[HeaderFieldCount];
        for (var i = 0; i < fields.Length && HasValue(reader, chunkEnd); i++)
            fields[i] = reader.ReadUInt32();

        return new AniHeader
        {
            HeaderSize = fields[0],
            NumFrames = fields[1],
            NumSteps = fields[2],
            Width = fields[3],
            Height = fields[4],
            BitCount = fields[5],
            NumPlanes = fields[6],
            DisplayRate = fields[7],
            Flags = AniHeaderFlags.FromUInt32(fields[8])
        };
    }

    private static List<uint> ReadValues(BinaryReader reader, long chunkEnd)
    {
        var values = new List<uint>();
        while (HasValue(reader, chunkEnd))
            values.Add(reader.ReadUInt32());

        return values;
    }

    private static List<AniFrameReference> ReadFrameReferences(BinaryReader reader, long listEnd)
    {
        var stream = reader.BaseStream;
        var frames = new List<AniFrameReference>();

        while (listEnd - stream.Position >= ChunkHeaderSize)
        {
            var subChunkId = ReadId(reader);
            var subChunkSize = reader.ReadUInt32();
            var dataStart = stream.Position;
            var dataEnd = dataStart + Padded(subChunkSize);

            if (subChunkId == "icon")
            {
                frames.Add(new AniFrameReference
                {
                    Offset = dataStart,
                    RealOffset = dataStart,
                    Size = (uint)(Math.Min(dataEnd, stream.Length) - dataStart)
                });
            }

            if (dataEnd > listEnd)
                break;

            stream.Position = dataEnd;
        }

        return frames;
    }

    private static void ReadInfo(BinaryReader reader, Dictionary<string, string> metaData, long listEnd)
    {
        var stream = reader.BaseStream;

        while (listEnd - stream.Position >= ChunkHeaderSize)
        {
            var subChunkId = ReadId(reader);
            var subChunkSize = reader.ReadUInt32();
            if (subChunkSize > listEnd - stream.Position)
                return;

            var text = Encoding.ASCII.GetString(reader.ReadBytes((int)subChunkSize)).TrimEnd('\0', ' ');
            if (!metaData.ContainsKey(subChunkId))
                metaData.Add(subChunkId, text);

            if (subChunkSize % 2 != 0 && stream.Position < listEnd)
                SkipPadByteIfPresent(reader);
        }
    }

    private static void SkipPadByteIfPresent(BinaryReader reader)
    {
        if (reader.ReadByte() != 0)
            reader.BaseStream.Position--;
    }
}
