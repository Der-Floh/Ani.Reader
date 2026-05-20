using Ani.Reader.Models;

using PeDecoder;
using PeDecoder.Models;

namespace Ani.Reader.Decoder;

/// <summary>
/// Decodes ANI cursor resources from PE files (executables and DLLs).
/// </summary>
public sealed class AniPeDecoder : IAniPeDecoder
{
    private readonly IPeDecoder _peDecoder;
    private readonly IAniDecoder _aniDecoder;

    /// <summary>
    /// Initializes a new instance of <see cref="AniPeDecoder"/> with the specified decoders.
    /// </summary>
    /// <param name="peDecoder">The PE file decoder used to extract resource data.</param>
    /// <param name="aniDecoder">The ANI decoder used to parse individual cursor resource bytes.</param>
    public AniPeDecoder(IPeDecoder peDecoder, IAniDecoder aniDecoder)
    {
        _peDecoder = peDecoder;
        _aniDecoder = aniDecoder;
    }

    /// <inheritdoc/>
    public DecodedAniResult? GetDecodedAniResult(Stream stream)
    {
        var mzHeader = _peDecoder.DecodeMZ(stream);
        if (!_peDecoder.IsPeFormat(mzHeader))
            return null;

        var peHeader = _peDecoder.DecodePE(stream);
        var resourceDirectory = _peDecoder.DecodeResourceDirectory(stream, peHeader);
        if (resourceDirectory is null)
            return null;

        var result = new DecodedAniResult
        {
            OriginFileType = peHeader.Characteristics.HasFlag(Characteristics.ImageFileDLL)
                ? AniOriginFileType.Dll
                : AniOriginFileType.Executable
        };

        AddAniCursors(result, resourceDirectory, peHeader, stream);
        return result;
    }

    /// <inheritdoc/>
    public bool IsPeFormat(Stream stream) => _peDecoder.IsPeFormat(stream);

    private void AddAniCursors(DecodedAniResult result, ResourceDirectory resourceDirectory, PE_Header peHeader, Stream stream)
    {
        var aniResources = resourceDirectory.GetResources(ResourceType.RT_ANICURSOR.ToString());
        if (aniResources is null)
            return;

        for (var i = 0; i < aniResources.Length; i++)
        {
            var resource = aniResources[i];
            var fileOffset = resource.GetFileOffset(stream, peHeader);
            var entry = _aniDecoder.Read(stream, fileOffset, resource.Size);
            if (entry is null)
                return;

            entry.Id = (int)resource.ID;
            result.Entries.Add(entry);
        }
    }
}
