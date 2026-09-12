using Ani.Reader.Decoder;

using Ico.Reader;
using Ico.Reader.Export;
using Ico.Reader.PeDecoder;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ani.Reader;

/// <summary>
/// Contains extension methods for configuring ANI reading services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds services necessary for reading and decoding ANI files.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddAniReader(this IServiceCollection services)
    {
        services.TryAddSingleton<IPeDecoder, PeFileDecoder>();
        services.TryAddSingleton<IAniDecoder, AniDecoder>();
        services.TryAddSingleton<IAniPeDecoder, AniPeDecoder>();

        services.AddSingleton(p =>
        {
            var configuration = new AniReaderConfiguration
            {
                AniDecoder = p.GetRequiredService<IAniDecoder>(),
                AniPeDecoder = p.GetRequiredService<IAniPeDecoder>(),
                IcoReader = p.GetService<IcoReader>() ?? new IcoReader(),
                IcoExporter = p.GetService<IIcoExporter>() ?? new IcoExporter()
            };

            return new AniReader(configuration);
        });

        return services;
    }
}
