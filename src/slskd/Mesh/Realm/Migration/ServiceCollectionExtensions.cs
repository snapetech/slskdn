// <copyright file="ServiceCollectionExtensions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Realm.Migration
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    ///     Extension methods for registering migration services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        ///     Adds realm migration services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddRealmMigrationServices(this IServiceCollection services)
        {
            // Register migration services
            services.AddSingleton<RealmChangeValidator>();
            services.AddSingleton<RealmMigrationTool>();

            return services;
        }
    }
}


