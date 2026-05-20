using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace RealWorldDownloader;

/// <summary>
/// Base class for downloading cursors and icons from rw-designer.com.
/// Fetches library index pages and downloads all linked files to a local directory,
/// organized by file type, with bounded concurrency.
/// </summary>
public abstract class RealWorldDownloader
{
    private const string BaseUrl = "http://www.rw-designer.com";
    private const int PageSize = 100;
    private const int DefaultConcurrency = 4;

    private static readonly Regex SanitizeRegex = new(@"[^a-zA-Z0-9\-._]", RegexOptions.Compiled);
    private static readonly Regex MultiUnderscoreRegex = new(@"_+", RegexOptions.Compiled);
    private static readonly Regex LeadingTrailingUnderscoreRegex = new(@"^_+|_+$", RegexOptions.Compiled);
    private static readonly Regex ContentDispositionFilenameRegex =
        new(@"filename[^;=\n]*=((['""]).*?\2|[^;\n]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly HttpClient _httpClient;
    private readonly int _concurrency;

    /// <summary>URL path segment identifying the library, e.g. "cursor-library" or "icon-library".</summary>
    protected abstract string LibrarySegment { get; }

    /// <summary>Prefix used when falling back to an ID-based filename, e.g. "cursor_" or "icon_".</summary>
    protected abstract string FilePrefix { get; }

    /// <param name="httpClient">Shared <see cref="HttpClient"/> instance. The caller is responsible for its lifetime.</param>
    /// <param name="concurrency">Maximum number of concurrent HTTP requests.</param>
    protected RealWorldDownloader(HttpClient httpClient, int concurrency = DefaultConcurrency)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _concurrency = concurrency > 0 ? concurrency : DefaultConcurrency;
    }

    /// <summary>
    /// Fetches up to <paramref name="count"/> items from the library and saves them to
    /// <paramref name="outputDirectory"/>, grouped into sub-folders by file type.
    /// </summary>
    /// <param name="count">Maximum number of items to download.</param>
    /// <param name="outputDirectory">Root directory for downloaded files.</param>
    /// <param name="progress">Optional progress reporter; receives both page-fetch and download progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DownloadSummary> DownloadAsync(
        int count,
        string outputDirectory,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "Must be greater than zero.");
        if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("Must not be empty.", nameof(outputDirectory));

        Directory.CreateDirectory(outputDirectory);

        // ── Phase 1: fetch index pages ──────────────────────────────────────────
        int pagesNeeded = (int)Math.Ceiling(count / (double)PageSize);
        var semaphore = new SemaphoreSlim(_concurrency, _concurrency);
        int completedPages = 0;

        var pageTasks = Enumerable.Range(0, pagesNeeded).Select(i =>
            FetchPageAsync(
                $"{BaseUrl}/{LibrarySegment}/{i * PageSize}",
                semaphore,
                () =>
                {
                    int done = Interlocked.Increment(ref completedPages);
                    progress?.Report(new DownloadProgress(ProgressType.Pages, done, pagesNeeded));
                },
                cancellationToken));

        PageResult[] pageResults = await Task.WhenAll(pageTasks).ConfigureAwait(false);

        List<string> downloadLinks = pageResults
            .Where(r => r.Success)
            .SelectMany(r => r.Links!)
            .ToList();

        // ── Phase 2: download files ─────────────────────────────────────────────
        int completedDownloads = 0;

        var downloadTasks = downloadLinks.Select(link =>
            DownloadItemAsync(
                link,
                outputDirectory,
                semaphore,
                () =>
                {
                    int done = Interlocked.Increment(ref completedDownloads);
                    progress?.Report(new DownloadProgress(ProgressType.Downloads, done, downloadLinks.Count));
                },
                cancellationToken));

        ItemResult[] downloadResults = await Task.WhenAll(downloadTasks).ConfigureAwait(false);

        // ── Aggregate results ───────────────────────────────────────────────────
        int successful = 0, failed = 0;
        var errors = new List<DownloadError>();
        var fileTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (ItemResult result in downloadResults)
        {
            if (result.Success)
            {
                successful++;
                if (result.FileType is not null)
                    fileTypes[result.FileType] = fileTypes.TryGetValue(result.FileType, out int c) ? c + 1 : 1;
            }
            else
            {
                failed++;
                if (result.Url is not null && result.Error is not null)
                    errors.Add(new DownloadError(result.Url, result.Error));
            }
        }

        return new DownloadSummary(successful, failed, errors, fileTypes);
    }

    // ── Private helpers ─────────────────────────────────────────────────────────

    private async Task<PageResult> FetchPageAsync(
        string url,
        SemaphoreSlim semaphore,
        Action onComplete,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string html = await _httpClient.GetStringAsync(url).ConfigureAwait(false);
            List<string> links = ExtractDownloadLinks(html);
            onComplete();
            return new PageResult(true, links, null);
        }
        catch (Exception ex)
        {
            return new PageResult(false, null, ex.Message);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<ItemResult> DownloadItemAsync(
        string url,
        string outputDirectory,
        SemaphoreSlim semaphore,
        Action onComplete,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using HttpResponseMessage response = await _httpClient
                .GetAsync(url, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            byte[] data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

            string filename =
                GetFilenameFromContentDisposition(response.Content.Headers.ContentDisposition)
                ?? GetFilenameFromUrl(url);

            string ext = FileTypeDetector.Detect(data);
            string typeDir = Path.Combine(outputDirectory, ext);
            Directory.CreateDirectory(typeDir);

            if (!filename.EndsWith($".{ext}", StringComparison.OrdinalIgnoreCase))
                filename = $"{filename}.{ext}";

            filename = SanitizeFilename(filename);

            await File.WriteAllBytesAsync(Path.Combine(typeDir, filename), data).ConfigureAwait(false);

            onComplete();
            return new ItemResult(true, filename, ext, null, null);
        }
        catch (Exception ex)
        {
            return new ItemResult(false, null, null, url, ex.Message);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static List<string> ExtractDownloadLinks(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        HtmlNodeCollection? nodes = doc.DocumentNode
            .SelectNodes("//a[contains(concat(' ', normalize-space(@class), ' '), ' download ')]");

        if (nodes is null)
            return [];

        var links = new List<string>(nodes.Count);
        foreach (HtmlNode node in nodes)
        {
            string href = node.GetAttributeValue("href", "");
            if (string.IsNullOrEmpty(href)) continue;
            links.Add(href.StartsWith("/", StringComparison.Ordinal) ? $"{BaseUrl}{href}" : href);
        }
        return links;
    }

    private static string? GetFilenameFromContentDisposition(ContentDispositionHeaderValue? cd)
    {
        if (cd is null) return null;

        // Prefer RFC 5987 encoded name, fall back to plain filename
        string? raw = cd.FileNameStar ?? cd.FileName;
        if (string.IsNullOrWhiteSpace(raw)) return null;

        return SanitizeFilename(raw.Trim('"', '\'', ' '));
    }

    private string GetFilenameFromUrl(string url)
    {
        Match m = Regex.Match(url, @"id=(\d+)");
        string id = m.Success ? m.Groups[1].Value : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        return SanitizeFilename($"{FilePrefix}{id}");
    }

    internal static string SanitizeFilename(string filename)
    {
        string result = SanitizeRegex.Replace(filename, "_");
        result = MultiUnderscoreRegex.Replace(result, "_");
        result = LeadingTrailingUnderscoreRegex.Replace(result, "");
        return result;
    }

    // ── Internal result records ──────────────────────────────────────────────────

    private sealed record PageResult(bool Success, List<string>? Links, string? Error);

    private sealed record ItemResult(
        bool Success,
        string? Filename,
        string? FileType,
        string? Url,
        string? Error);
}
