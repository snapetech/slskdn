// <copyright file="ServiceCollectionExtensions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Governance
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    ///     Extension methods for registering governance services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        ///     Adds governance services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddGovernanceServices(this IServiceCollection services)
        {
            // Register governance client
            services.AddSingleton<IRealmAwareGovernanceClient, RealmAwareGovernanceClient>();
            services.AddSingleton<IGovernanceClient>(sp => sp.GetRequiredService<IRealmAwareGovernanceClient>());

            return services;
        }
    }
}

