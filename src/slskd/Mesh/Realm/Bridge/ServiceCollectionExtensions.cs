// <copyright file="ServiceCollectionExtensions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Realm.Bridge
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    ///     Extension methods for registering bridge services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        ///     Adds bridge services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddBridgeServices(this IServiceCollection services)
        {
            // Register bridge flow enforcer
            services.AddSingleton<BridgeFlowEnforcer>();

            // Register ActivityPub bridge
            services.AddSingleton<ActivityPubBridge>();

            // Register metadata bridge
            services.AddSingleton<MetadataBridge>();

            return services;
        }
    }
}
