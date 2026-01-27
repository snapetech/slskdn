// <copyright file="ServiceCollectionExtensions.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Core;

using Microsoft.Extensions.DependencyInjection;
using slskd.VirtualSoulfind.Core.Book;
using slskd.VirtualSoulfind.Core.GenericFile;
using slskd.VirtualSoulfind.Core.Movie;
using slskd.VirtualSoulfind.Core.Music;
using slskd.VirtualSoulfind.Core.Tv;

/// <summary>
///     Extension methods for registering VirtualSoulfind Core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the content domain provider registry and registers all built-in domain providers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddContentDomainProviders(this IServiceCollection services)
    {
        // Register the registry
        services.AddSingleton<IContentDomainProviderRegistry, ContentDomainProviderRegistry>();

            // Register all built-in providers with the registry after they're created
            services.AddSingleton(sp =>
            {
                var registry = sp.GetRequiredService<IContentDomainProviderRegistry>();

                // Register Music provider (if available)
                var musicProvider = sp.GetService<IMusicContentDomainProvider>();
                if (musicProvider != null)
                {
                    registry.RegisterProvider(ContentDomainProviderAdapters.CreateMusicAdapter(musicProvider));
                }

                // Register Book provider
                var bookProvider = sp.GetRequiredService<IBookContentDomainProvider>();
                registry.RegisterProvider(ContentDomainProviderAdapters.CreateBookAdapter(bookProvider));

                // Register Movie provider
                var movieProvider = sp.GetRequiredService<IMovieContentDomainProvider>();
                registry.RegisterProvider(ContentDomainProviderAdapters.CreateMovieAdapter(movieProvider));

                // Register TV provider
                var tvProvider = sp.GetRequiredService<ITvContentDomainProvider>();
                registry.RegisterProvider(ContentDomainProviderAdapters.CreateTvAdapter(tvProvider));

                // Register GenericFile provider
                var genericFileProvider = sp.GetRequiredService<GenericFile.IGenericFileContentDomainProvider>();
                registry.RegisterProvider(ContentDomainProviderAdapters.CreateGenericFileAdapter(genericFileProvider));

                return registry;
            });

        return services;
    }
}
