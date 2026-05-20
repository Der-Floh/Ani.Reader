namespace RealWorldDownloader;

public enum ProgressType
{
    Pages,
    Downloads
}

/// <summary>Progress report emitted by <see cref="RealWorldDownloader"/>.</summary>
public sealed class DownloadProgress
{
    public ProgressType Type { get; }
    public int Completed { get; }
    public int Total { get; }
    public double Percentage => Total > 0 ? (double)Completed / Total * 100.0 : 0.0;

    public DownloadProgress(ProgressType type, int completed, int total)
    {
        Type = type;
        Completed = completed;
        Total = total;
    }

    public override string ToString() =>
        $"{Type}: {Completed}/{Total} ({Percentage:F0}%)";
}
