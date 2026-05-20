namespace RealWorldDownloader;

/// <summary>
/// Downloads icons from the rw-designer.com icon library
/// (<c>http://www.rw-designer.com/icon-library/</c>).
/// Files are saved to sub-folders named after their detected file type (e.g. <c>ico/</c>, <c>png/</c>).
/// </summary>
public sealed class RealWorldIconDownloader : RealWorldDownloader
{
    protected override string LibrarySegment => "icon-library";
    protected override string FilePrefix => "icon_";

    /// <param name="httpClient">Shared <see cref="HttpClient"/> instance.</param>
    /// <param name="concurrency">Maximum concurrent requests (default: 4).</param>
    public RealWorldIconDownloader(HttpClient httpClient, int concurrency = 4)
        : base(httpClient, concurrency) { }
}
