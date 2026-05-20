namespace RealWorldDownloader;

/// <summary>Summary of a completed download batch.</summary>
public sealed class DownloadSummary
{
    public int Successful { get; }
    public int Failed { get; }
    public IReadOnlyList<DownloadError> Errors { get; }

    /// <summary>Number of downloaded files keyed by file extension (e.g. "ani", "cur", "png").</summary>
    public IReadOnlyDictionary<string, int> FileTypes { get; }

    public DownloadSummary(
        int successful,
        int failed,
        IReadOnlyList<DownloadError> errors,
        IReadOnlyDictionary<string, int> fileTypes)
    {
        Successful = successful;
        Failed = failed;
        Errors = errors;
        FileTypes = fileTypes;
    }
}
