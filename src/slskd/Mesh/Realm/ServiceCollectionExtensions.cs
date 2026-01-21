// <copyright file="ServiceCollectionExtensions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Realm
{
    using Microsoft.Extensions.DependencyInjection;
    using slskd.Mesh.Realm.Migration;

    /// <summary>
    ///     Extension methods for registering realm services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        ///     Adds realm services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddRealmServices(this IServiceCollection services)
        {
            // Register concrete service first
            services.AddSingleton<RealmService>();

            // Bind interface to same singleton instance
            services.AddSingleton<IRealmService>(sp => sp.GetRequiredService<RealmService>());

            // Hosted service uses concrete RealmService
            services.AddHostedService<RealmHostedService>();

            // Register multi-realm service (T-REALM-02)
            services.AddSingleton<MultiRealmService>();

            // Register multi-realm service as hosted service for initialization
            services.AddHostedService<MultiRealmHostedService>();

            // Register migration services (T-REALM-05)
            services.AddRealmMigrationServices();

            return services;
        }
    }
}
