using System.Text;

using Ani.Reader.Models;

namespace Ani.Reader.Decoder;

/// <summary>
/// Provides functionality for decoding ANI animation data from RIFF streams.
/// </summary>
public sealed class AniDecoder : IAniDecoder
{
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

    private static AniEntry ParseFromStream(Stream stream)
    {
        var reader = new BinaryReader(stream, Encoding.ASCII);

        var riff = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (riff != "RIFF")
            throw new InvalidDataException("Not a valid ANI file: missing RIFF header.");

        var fileSize = reader.ReadUInt32();
        var acon = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (acon != "ACON")
            throw new InvalidDataException("Not a valid ANI file: missing ACON marker.");

        var entry = new AniEntry();

        while (stream.Position < fileSize)
        {
            var chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (chunkId == string.Empty)
                break;

            var chunkSize = reader.ReadUInt32();
            var chunkStart = stream.Position;

            switch (chunkId)
            {
                case "\0ani":
                case "anih":
                    entry.Header = ReadAnihChunk(reader);
                    break;
                case "rate":
                    entry.FrameRates = ReadUintArray(reader, chunkSize);
                    break;
                case "seq ":
                    entry.FrameSequence = ReadUintArray(reader, chunkSize);
                    break;
                case "LIST":
                    var listType = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    if (listType == "fram")
                    {
                        entry.Frames.AddRange(ReadFrameReferences(entry, reader, chunkSize - 4));
                    }
                    else if (listType == "INFO")
                    {
                        ReadInfoChunk(entry, reader, chunkSize - 4);
                        chunkSize = (uint)(stream.Position - chunkStart);
                    }
                    break;
                default:
                    stream.Seek(chunkSize, SeekOrigin.Current);
                    break;
            }

            stream.Position = chunkStart + chunkSize;
        }

        return entry;
    }

    private static void ReadInfoChunk(AniEntry entry, BinaryReader reader, uint chunkSize)
    {
        var endPos = reader.BaseStream.Position + chunkSize;
        while (reader.BaseStream.Position < endPos)
        {
            var subChunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var subChunkSize = reader.ReadUInt32();
            if (subChunkSize % 2 != 0)
                subChunkSize++;

            var subChunkData = reader.ReadBytes((int)subChunkSize);
            var subChunkText = Encoding.ASCII.GetString(subChunkData).TrimEnd('\0', ' ');
            entry.MetaData.Add(subChunkId, subChunkText);
        }
    }

    private static AniHeader ReadAnihChunk(BinaryReader reader)
    {
        var headerSize = reader.ReadUInt32();
        var header = new AniHeader
        {
            HeaderSize = headerSize,
            NumFrames = reader.ReadUInt32(),
            NumSteps = reader.ReadUInt32(),
            Width = reader.ReadUInt32(),
            Height = reader.ReadUInt32(),
            BitCount = reader.ReadUInt32(),
            NumPlanes = reader.ReadUInt32(),
            DisplayRate = reader.ReadUInt32()
        };

        header.Flags = AniHeaderFlags.FromUInt32(reader.ReadUInt32());
        return header;
    }

    private static List<uint> ReadUintArray(BinaryReader reader, uint size)
    {
        var values = new List<uint>();
        for (var i = 0; i < size / 4; i++)
            values.Add(reader.ReadUInt32());
        return values;
    }

    private static List<AniFrameReference> ReadFrameReferences(AniEntry entry, BinaryReader reader, uint chunkSize)
    {
        var frames = new List<AniFrameReference>();
        var endPos = reader.BaseStream.Position + chunkSize;

        while (reader.BaseStream.Position < endPos)
        {
            var subChunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (entry.Header.NumFrames == frames.Count && subChunkId != "icon")
                break;

            var subChunkSize = reader.ReadUInt32();
            if (subChunkSize % 2 != 0)
                subChunkSize++;

            frames.Add(new AniFrameReference
            {
                Offset = reader.BaseStream.Position,
                RealOffset = reader.BaseStream.Position,
                Size = subChunkSize
            });

            reader.BaseStream.Seek(subChunkSize, SeekOrigin.Current);
        }

        return frames;
    }
}
