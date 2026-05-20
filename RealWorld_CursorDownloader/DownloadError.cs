namespace RealWorldDownloader;

/// <summary>Represents a failed download attempt.</summary>
public sealed class DownloadError
{
    public string Url { get; }
    public string Message { get; }

    public DownloadError(string url, string message)
    {
        Url = url;
        Message = message;
    }

    public override string ToString() => $"{Url}: {Message}";
}
