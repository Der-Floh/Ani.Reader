using Ani.Reader.Models;

using Ico.Reader.Data.Source;

namespace Ani.Reader;

/// <summary>
/// Provides functionality to read ANI animation data from files, byte arrays, or streams.
/// </summary>
/// <remarks>
/// Animations whose icon flag is clear, and animations in which no step shows a frame holding an image, are left out.
/// A standalone .ani file of either kind reads as <see langword="null"/>, and an executable or DLL returns the
/// animations that remain.
/// </remarks>
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

        var result = new DecodedAniResult { OriginFileType = AniOriginFileType.Ani };
        result.Entries.Add(aniEntry);

        var animations = CreateArray(result, dataSource, name);
        return animations.Length == 0 ? null : animations;
    }

    private AniData[] CreateArray(DecodedAniResult decodedAniResult, IDataSource dataSource, string name)
    {
        var animations = new List<AniData>(decodedAniResult.Entries.Count);
        foreach (var entry in decodedAniResult.Entries)
        {
            if (!entry.Header.Flags.IconFlag)
                continue;

            var aniData = new AniData(entry, decodedAniResult.OriginFileType, dataSource, _configuration.IcoReader, _configuration.IcoExporter);
            if (!aniData.ShowsAnImage)
                continue;

            aniData.Name = decodedAniResult.OriginFileType is AniOriginFileType.Executable or AniOriginFileType.Dll
                ? $"{name} ({entry.Id})"
                : name;

            animations.Add(aniData);
        }

        return [.. animations];
    }
}
