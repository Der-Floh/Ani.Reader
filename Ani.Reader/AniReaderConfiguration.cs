using Ani.Reader.Decoder;

using Ico.Reader;
using Ico.Reader.Export;
using Ico.Reader.PeDecoder;

namespace Ani.Reader;

/// <summary>
/// Configuration settings for <see cref="AniReader"/>, allowing customization of the decoding pipeline.
/// </summary>
public sealed class AniReaderConfiguration
{
    /// <summary>
    /// Gets or sets the ANI RIFF decoder used to parse ANI file data.
    /// </summary>
    public IAniDecoder AniDecoder { get; set; } = new AniDecoder();

    /// <summary>
    /// Gets or sets the PE decoder used to extract ANI cursor resources from executables and DLLs.
    /// </summary>
    public IAniPeDecoder AniPeDecoder { get; set; } = new AniPeDecoder(new PeFileDecoder(), new AniDecoder());

    /// <summary>
    /// Gets or sets the ICO reader used to decode individual animation frames within an ANI file.
    /// </summary>
    public IcoReader IcoReader { get; set; } = new IcoReader();

    /// <summary>
    /// Gets or sets the ICO exporter used to write decoded animation frames to disk.
    /// </summary>
    public IIcoExporter IcoExporter { get; set; } = new IcoExporter();
}
