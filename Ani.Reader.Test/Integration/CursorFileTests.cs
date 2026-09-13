using Ani.Reader.Test.Fixtures;
using Ani.Reader.Test.Imaging;

using Ico.Reader;
using Ico.Reader.Data;

using Xunit;

namespace Ani.Reader.Test.Integration;

/// <summary>
/// The .cur fixtures are not ANI sources; they cover the cursor-decoding path that
/// <see cref="Models.AniData"/> runs for every frame, using files whose content is known.
/// </summary>
public sealed class CursorFileTests
{
    private static IcoData Load(string fileName) =>
        new IcoReader().Read(TestFiles.PathTo(fileName))
            ?? throw new InvalidOperationException($"{fileName} failed to read as cursor data.");

    [Theory]
    [InlineData(TestFiles.StaticBig)]
    [InlineData(TestFiles.StaticBigTransparent)]
    [InlineData(TestFiles.StaticMultiSize)]
    [InlineData(TestFiles.StaticMultiSizeTransparent)]
    public void ImageReferences_MatchTheFixture(string fileName)
    {
        var fixture = CursorFixtures.For(fileName);
        var references = Load(fileName).ImageReferences.OrderBy(r => r.Width).ToArray();

        Assert.Equal(fixture.Sizes.Count, references.Length);
        Assert.Equal(fixture.Sizes.Select(s => s.Width), references.Select(r => r.Width));
        Assert.Equal(fixture.Sizes.Select(s => s.Height), references.Select(r => r.Height));
        Assert.Equal(fixture.Sizes.Select(s => s.HotspotX), references.Select(r => r.HotspotX));
        Assert.Equal(fixture.Sizes.Select(s => s.HotspotY), references.Select(r => r.HotspotY));
    }

    [Theory]
    [InlineData(TestFiles.StaticBig)]
    [InlineData(TestFiles.StaticBigTransparent)]
    [InlineData(TestFiles.StaticMultiSize)]
    [InlineData(TestFiles.StaticMultiSizeTransparent)]
    public void ImageReferences_ReportTheStoredEncoding(string fileName)
    {
        var fixture = CursorFixtures.For(fileName);
        var references = Load(fileName).ImageReferences.OrderBy(r => r.Width).ToArray();

        Assert.Equal(fixture.Sizes.Select(s => s.ExpectedFormat), references.Select(r => r.Format));
    }

    [Theory]
    [InlineData(TestFiles.StaticBig)]
    [InlineData(TestFiles.StaticBigTransparent)]
    [InlineData(TestFiles.StaticMultiSize)]
    [InlineData(TestFiles.StaticMultiSizeTransparent)]
    public void CursorFiles_ExposeASingleCursorGroup(string fileName)
    {
        var icoData = Load(fileName);

        Assert.Single(icoData.CursorGroups);
        Assert.Empty(icoData.IconGroups);
        Assert.Equal(IcoOriginFileType.Cur, icoData.OriginFileType);
    }

    [Theory]
    [InlineData(TestFiles.StaticBig)]
    [InlineData(TestFiles.StaticBigTransparent)]
    [InlineData(TestFiles.StaticMultiSize)]
    [InlineData(TestFiles.StaticMultiSizeTransparent)]
    public async Task DecodedImages_MatchTheFixturePixels(string fileName)
    {
        var fixture = CursorFixtures.For(fileName);
        var icoData = Load(fileName);

        foreach (var size in fixture.Sizes)
        {
            var reference = icoData.ImageReferences.Single(r => r.Width == size.Width);
            using var image = Pixels.Decode(await icoData.GetImageAsync(reference, TestContext.Current.CancellationToken));

            Assert.Equal(size.Width, (int)image.Width);
            Assert.Equal(size.Height, (int)image.Height);
            Assert.Equal(size.TransparentPixelsPerFrame[0], Pixels.CountTransparent(image));

            var foreground = Pixels.CountMatching(image, CursorFixtures.Foreground);
            var background = Pixels.CountMatching(image, CursorFixtures.Background);
            Assert.True(foreground > 0, $"{fileName} {size} has no foreground pixels.");
            Assert.True(background > 0, $"{fileName} {size} has no background pixels.");

            if (size.TransparentPixelsPerFrame[0] == 0)
                Assert.Equal(size.Width * size.Height, foreground + background);
        }
    }

    [Theory]
    [InlineData(TestFiles.StaticBigTransparent)]
    [InlineData(TestFiles.StaticMultiSizeTransparent)]
    public async Task TransparentFixtures_HaveATransparentCentre(string fileName)
    {
        var fixture = CursorFixtures.For(fileName);
        var icoData = Load(fileName);

        foreach (var size in fixture.Sizes)
        {
            var reference = icoData.ImageReferences.Single(r => r.Width == size.Width);
            using var image = Pixels.Decode(await icoData.GetImageAsync(reference, TestContext.Current.CancellationToken));

            Assert.Equal(0, (int)Pixels.Center(image).A);
        }
    }

    [Theory]
    [InlineData(TestFiles.StaticBig)]
    [InlineData(TestFiles.StaticMultiSize)]
    public async Task OpaqueFixtures_HaveNoTransparentPixels(string fileName)
    {
        var fixture = CursorFixtures.For(fileName);
        var icoData = Load(fileName);

        foreach (var size in fixture.Sizes)
        {
            var reference = icoData.ImageReferences.Single(r => r.Width == size.Width);
            using var image = Pixels.Decode(await icoData.GetImageAsync(reference, TestContext.Current.CancellationToken));

            Assert.Equal(0, Pixels.CountTransparent(image));
            Assert.Equal(size.Width * size.Height, Pixels.CountOpaque(image));
        }
    }
}
