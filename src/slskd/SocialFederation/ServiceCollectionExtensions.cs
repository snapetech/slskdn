// <copyright file="ServiceCollectionExtensions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SocialFederation
{
    using System;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using slskd.VirtualSoulfind.Core;

    /// <summary>
    ///     Extension methods for registering SocialFederation services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        ///     Adds SocialFederation services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddSocialFederation(this IServiceCollection services)
        {
            // Register configuration options
            services.AddOptions<FederationPublishingOptions>();

            // Register the key store with data protection
            services.AddSingleton<IActivityPubKeyStore>(sp =>
            {
                var dataProtector = sp.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("ActivityPubKeyStore");
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ActivityPubKeyStore>>();
                return new ActivityPubKeyStore(dataProtector, logger);
            });

            // Register activity delivery service
            services.AddSingleton<ActivityDeliveryService>(sp =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("FederationDelivery");
                var federationOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<SocialFederationOptions>>();
                var publishingOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<FederationPublishingOptions>>();
                var keyStore = sp.GetRequiredService<IActivityPubKeyStore>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ActivityDeliveryService>>();

                return new ActivityDeliveryService(httpClient, federationOptions, publishingOptions, keyStore, logger);
            });

            // Register content domain providers (if available)
            // Note: These would typically be registered in the main application
            // For now, we make them optional

            // Register the music library actor (depends on music provider)
            services.AddSingleton<MusicLibraryActor?>(sp =>
            {
                try
                {
                    var federationOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<SocialFederationOptions>>();
                    var keyStore = sp.GetRequiredService<IActivityPubKeyStore>();
                    var musicProvider = sp.GetService<VirtualSoulfind.Core.Music.IMusicContentDomainProvider>();
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MusicLibraryActor>>();

                    // Only create if we have a music provider
                    return musicProvider != null
                        ? new MusicLibraryActor(federationOptions, keyStore, musicProvider, logger)
                        : null;
                }
                catch
                {
                    // Return null if dependencies are not available
                    return null;
                }
            });

            // Register the library actor service
            services.AddSingleton<LibraryActorService>(sp =>
            {
                var federationOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<SocialFederationOptions>>();
                var keyStore = sp.GetRequiredService<IActivityPubKeyStore>();
                var musicActor = sp.GetService<MusicLibraryActor>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LibraryActorService>>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                return new LibraryActorService(federationOptions, keyStore, musicActor, logger, loggerFactory);
            });

            // Register federation service
            services.AddSingleton<FederationService>(sp =>
            {
                var federationOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<SocialFederationOptions>>();
                var publishingOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<FederationPublishingOptions>>();
                var libraryActorService = sp.GetRequiredService<LibraryActorService>();
                var keyStore = sp.GetRequiredService<IActivityPubKeyStore>();
                var deliveryService = sp.GetRequiredService<ActivityDeliveryService>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FederationService>>();

                return new FederationService(federationOptions, publishingOptions, libraryActorService, keyStore, deliveryService, logger);
            });

            // Register VirtualSoulfind federation integration
            services.AddSingleton<VirtualSoulfindFederationIntegration>(sp =>
            {
                var publishingOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<FederationPublishingOptions>>();
                var federationService = sp.GetRequiredService<FederationService>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<VirtualSoulfindFederationIntegration>>();

                return new VirtualSoulfindFederationIntegration(publishingOptions, federationService, logger);
            });

            // Register controllers (ASP.NET Core will auto-discover them)
            // The controllers are in the API namespace and will be found by the framework

            return services;
        }
    }
}
