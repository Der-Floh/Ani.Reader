using Ani.Reader.Test.Fixtures;

using Ico.Reader.Data;
using Ico.Reader.Export;

using Xunit;

namespace Ani.Reader.Test.Integration;

/// <summary>
/// A caller on a UI thread may wait for a task on that thread, so the library must not resume on the caller's
/// synchronization context.
/// </summary>
public sealed class CallerContextTests
{
    [Fact]
    public async Task SaveImages_DoesNotResumeOnTheCallersContext()
    {
        var reader = new AniReader(new AniReaderConfiguration { IcoExporter = new YieldingExporter() });
        var aniData = Assert.Single(reader.Read(TestFiles.PathTo(TestFiles.AnimatedThreeFrame))!);
        using var directory = new TemporaryDirectory();

        var saving = StartOnBlockedContext(() => aniData.SaveImages(directory.Path, aniData.Animations[0]));

        var timeout = Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.Same(saving, await Task.WhenAny(saving, timeout));
    }

    private static Task StartOnBlockedContext(Func<Task> start)
    {
        var callerContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new BlockedContext());
        try
        {
            return start();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(callerContext);
        }
    }

    /// <summary>
    /// A context whose thread is busy waiting, so nothing posted to it ever runs.
    /// </summary>
    private sealed class BlockedContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
        }
    }

    private sealed class YieldingExporter : IIcoExporter
    {
        public async Task SaveImageAsync(IcoData icoData, ImageReference imageReference, string path, CancellationToken cancellationToken = default)
            => await Task.Delay(1, cancellationToken).ConfigureAwait(false);

        public Task SaveGroupToDirectoryAsync(IcoData icoData, IIcoGroup group, string path, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SaveAllGroupsToDirectoryAsync(IcoData icoData, string path, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SaveAllImagesToDirectoryAsync(IcoData icoData, string path, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
