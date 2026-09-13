using Ani.Reader.Decoder;
using Ani.Reader.Test.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Ani.Reader.Test.Unit;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAniReader_ResolvesAReaderThatReadsAnAnimation()
    {
        using var provider = new ServiceCollection().AddAniReader().BuildServiceProvider();

        var reader = provider.GetRequiredService<AniReader>();

        var aniData = Assert.Single(reader.Read(TestFiles.PathTo(TestFiles.AnimatedThreeFrame))!);
        Assert.Equal(3, aniData.TotalFrames);
    }

    [Fact]
    public void AddAniReader_KeepsADecoderRegisteredBeforeIt()
    {
        var decoder = new AniDecoder();
        using var provider = new ServiceCollection().AddSingleton<IAniDecoder>(decoder).AddAniReader().BuildServiceProvider();

        Assert.Same(decoder, provider.GetRequiredService<IAniDecoder>());
        Assert.Same(provider.GetRequiredService<AniReader>(), provider.GetRequiredService<AniReader>());
    }
}
