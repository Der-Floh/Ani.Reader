using Ani.Reader.Models;

using Ico.Reader.Data.Source;

namespace Ani.Reader;

/// <summary>
/// Provides functionality to read ANI animation data from files, byte arrays, or streams.
/// </summary>
public sealed class AniReader
{
    private readonly AniReaderConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of <see cref="AniReader"/> with default configuration settings.
    /// </summary>
    public AniReader()
    {
        _configuration = new AniReaderConfiguration();
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AniReader"/> with the specified configuration.
    /// </summary>
    /// <param name="configuration">The configuration settings to use for reading ANI files.</param>
    public AniReader(AniReaderConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Reads ANI data from the specified file path.
    /// </summary>
    /// <param name="filePath">The path to the file to read.</param>
    /// <returns>An array of <see cref="AniData"/> objects, or <see langword="null"/> if the file does not exist or cannot be read.</returns>
    public AniData[]? Read(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        return Read(new PathSource(filePath), Path.GetFileNameWithoutExtension(filePath));
    }

    /// <summary>
    /// Reads ANI data from a byte array.
    /// </summary>
    /// <param name="data">The byte array containing ANI data.</param>
    /// <returns>An array of <see cref="AniData"/> objects, or <see langword="null"/> if the data cannot be read.</returns>
    public AniData[]? Read(byte[] data) => Read(new MemorySource(data), string.Empty);

    /// <summary>
    /// Reads ANI data from a stream.
    /// </summary>
    /// <param name="stream">The stream containing ANI data.</param>
    /// <param name="copyStream">If <see langword="true"/>, a copy of the stream is made for safe access; if <see langword="false"/>, the original stream is used and must remain open.</param>
    /// <returns>An array of <see cref="AniData"/> objects, or <see langword="null"/> if the data cannot be read.</returns>
    public AniData[]? Read(Stream stream, bool copyStream = true)
    {
        if (!copyStream && !stream.CanSeek)
            throw new ArgumentException("A non-seekable stream must be copied. Call with copyStream: true.", nameof(stream));

        IDataSource dataSource = copyStream
            ? new StreamBufferSource(stream)
            : new SharedStreamSource(stream);

        return Read(dataSource, string.Empty);
    }

    private AniData[]? Read(IDataSource dataSource, string name)
    {
        using var stream = dataSource.GetStream();

        return _configuration.AniPeDecoder.IsPeFormat(stream)
            ? ReadFromPeFile(stream, dataSource, name)
            : ReadFromAniFile(dataSource, name);
    }

    private AniData[]? ReadFromPeFile(Stream stream, IDataSource dataSource, string name)
    {
        DecodedAniResult? decodedResult;
        try
        {
            decodedResult = _configuration.AniPeDecoder.GetDecodedAniResult(stream);
        }
        catch (Exception exception) when (exception is EndOfStreamException or InvalidDataException)
        {
            // A malformed executable is a parse failure, and every Read overload reports those as null.
            return null;
        }

        return decodedResult is null ? null : CreateArray(decodedResult, dataSource, name);
    }

    private AniData[]? ReadFromAniFile(IDataSource dataSource, string name)
    {
        using var aniStream = dataSource.GetStream();
        var aniEntry = _configuration.AniDecoder.Read(aniStream);
        if (aniEntry is null)
            return null;

        if (!aniEntry.Header.Flags.IconFlag)
            return null;

        var result = new DecodedAniResult { OriginFileType = AniOriginFileType.Ani };
        result.Entries.Add(aniEntry);
        return CreateArray(result, dataSource, name);
    }

    private AniData[] CreateArray(DecodedAniResult decodedAniResult, IDataSource dataSource, string name)
    {
        var array = new AniData[decodedAniResult.Entries.Count];
        for (var i = 0; i < decodedAniResult.Entries.Count; i++)
        {
            array[i] = new AniData(decodedAniResult.Entries[i], decodedAniResult.OriginFileType, dataSource, _configuration.IcoReader, _configuration.IcoExporter);
            if (decodedAniResult.OriginFileType is AniOriginFileType.Executable or AniOriginFileType.Dll)
            {
                array[i].Name = $"{name} ({decodedAniResult.Entries[i].Id})";
            }
            else
            {
                array[i].Name = name;
                if (i != 0)
                    name += $"_{i}";
            }
        }

        return array;
    }
}
