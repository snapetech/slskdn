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
            services.AddSingleton<IRealmAwareGovernanceClient>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RealmAwareGovernanceClient>>();
                var cfg = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<slskd.Mesh.Realm.MultiRealmConfig>>().CurrentValue;

                if (cfg?.Realms != null && cfg.Realms.Length > 1)
                {
                    var multiRealm = sp.GetRequiredService<slskd.Mesh.Realm.MultiRealmService>();
                    return new RealmAwareGovernanceClient(multiRealm, logger);
                }

                var realmService = sp.GetRequiredService<slskd.Mesh.Realm.IRealmService>();
                return new RealmAwareGovernanceClient(realmService, logger);
            });

            services.AddSingleton<IGovernanceClient>(sp => sp.GetRequiredService<IRealmAwareGovernanceClient>());
            return services;
        }
    }
}


