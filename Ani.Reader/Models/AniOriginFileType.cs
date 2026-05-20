namespace Ani.Reader.Models;

/// <summary>
/// Specifies the type of file from which the ANI animation originates.
/// </summary>
public enum AniOriginFileType
{
    /// <summary>
    /// The file is an executable file.
    /// </summary>
    Executable,

    /// <summary>
    /// The file is a DLL file.
    /// </summary>
    Dll,

    /// <summary>
    /// The file is an ani file.
    /// </summary>
    Ani,
}
