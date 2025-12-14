// <copyright file="ServiceCollectionExtensions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Gossip
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    ///     Extension methods for registering gossip services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        ///     Adds gossip services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddGossipServices(this IServiceCollection services)
        {
            // Register gossip service
            services.AddSingleton<IRealmAwareGossipService, RealmAwareGossipService>();
            services.AddSingleton<IGossipService>(sp => sp.GetRequiredService<IRealmAwareGossipService>());

            return services;
        }
    }
}
