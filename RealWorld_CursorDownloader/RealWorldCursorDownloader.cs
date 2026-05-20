namespace RealWorldDownloader;

/// <summary>
/// Downloads animated cursors and cursor sets from the rw-designer.com cursor library
/// (<c>http://www.rw-designer.com/cursor-library/</c>).
/// Files are saved to sub-folders named after their detected file type (e.g. <c>ani/</c>, <c>cur/</c>).
/// </summary>
public sealed class RealWorldCursorDownloader : RealWorldDownloader
{
    protected override string LibrarySegment => "cursor-library";
    protected override string FilePrefix => "cursor_";

    /// <param name="httpClient">Shared <see cref="HttpClient"/> instance.</param>
    /// <param name="concurrency">Maximum concurrent requests (default: 4).</param>
    public RealWorldCursorDownloader(HttpClient httpClient, int concurrency = 4)
        : base(httpClient, concurrency) { }
}
