// <copyright file="SecurityServiceExtensions.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Extension methods for registering security services.
/// </summary>
public static class SecurityServiceExtensions
{
    /// <summary>
    /// Add all security services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSecurityServices(
        this IServiceCollection services,
        Action<SecurityServiceRegistrationOptions>? configure = null)
    {
        var options = new SecurityServiceRegistrationOptions();
        configure?.Invoke(options);

        // Register options
        services.TryAddSingleton(options);

        // Core security services
        if (options.EnableNetworkGuard)
        {
            services.TryAddSingleton<NetworkGuard>();
        }

        if (options.EnableViolationTracker)
        {
            services.TryAddSingleton<ViolationTracker>();
        }

        if (options.EnableConnectionFingerprint)
        {
            services.TryAddSingleton<ConnectionFingerprintService>();
        }

        if (options.EnablePeerReputation)
        {
            services.TryAddSingleton<PeerReputation>();
        }

        // Advanced security services
        if (options.EnableParanoidMode)
        {
            services.TryAddSingleton<ParanoidMode>();
        }

        if (options.EnableCryptographicCommitment)
        {
            services.TryAddSingleton<CryptographicCommitment>();
        }

        if (options.EnableProofOfStorage)
        {
            services.TryAddSingleton<ProofOfStorage>();
        }

        if (options.EnableByzantineConsensus)
        {
            services.TryAddSingleton<ByzantineConsensus>();
        }

        if (options.EnableProbabilisticVerification)
        {
            services.TryAddSingleton<ProbabilisticVerification>();
        }

        // Intelligence services
        if (options.EnableEntropyMonitor)
        {
            services.TryAddSingleton<EntropyMonitor>();
        }

        if (options.EnableTemporalConsistency)
        {
            services.TryAddSingleton<TemporalConsistency>();
        }

        if (options.EnableFingerprintDetection)
        {
            services.TryAddSingleton<FingerprintDetection>();
        }

        if (options.EnableHoneypot)
        {
            services.TryAddSingleton<Honeypot>();
        }

        if (options.EnableCanaryTraps)
        {
            services.TryAddSingleton<CanaryTraps>();
        }

        if (options.EnableAsymmetricDisclosure)
        {
            services.TryAddSingleton<AsymmetricDisclosure>();
        }

        // Event aggregation
        services.TryAddSingleton<ISecurityEventSink, SecurityEventAggregator>();

        // Composite service for easy access
        services.TryAddSingleton<SecurityServices>();

        return services;
    }

    /// <summary>
    /// Add minimal security services (essential only).
    /// </summary>
    public static IServiceCollection AddMinimalSecurityServices(this IServiceCollection services)
    {
        return services.AddSecurityServices(options =>
        {
            options.EnableNetworkGuard = true;
            options.EnableViolationTracker = true;
            options.EnablePeerReputation = true;
            options.EnableParanoidMode = false;
            options.EnableCryptographicCommitment = false;
            options.EnableProofOfStorage = false;
            options.EnableByzantineConsensus = false;
            options.EnableProbabilisticVerification = false;
            options.EnableEntropyMonitor = false;
            options.EnableTemporalConsistency = false;
            options.EnableFingerprintDetection = false;
            options.EnableHoneypot = false;
            options.EnableCanaryTraps = false;
            options.EnableAsymmetricDisclosure = false;
        });
    }

    /// <summary>
    /// Add full security services (all features).
    /// </summary>
    public static IServiceCollection AddFullSecurityServices(this IServiceCollection services)
    {
        return services.AddSecurityServices(options =>
        {
            options.EnableAll();
        });
    }
}

/// <summary>
/// Options for configuring which security services to enable during DI registration.
/// </summary>
public sealed class SecurityServiceRegistrationOptions
{
    /// <summary>Gets or sets whether to enable network guard.</summary>
    public bool EnableNetworkGuard { get; set; } = true;

    /// <summary>Gets or sets whether to enable violation tracker.</summary>
    public bool EnableViolationTracker { get; set; } = true;

    /// <summary>Gets or sets whether to enable connection fingerprinting.</summary>
    public bool EnableConnectionFingerprint { get; set; } = true;

    /// <summary>Gets or sets whether to enable peer reputation.</summary>
    public bool EnablePeerReputation { get; set; } = true;

    /// <summary>Gets or sets whether to enable paranoid mode.</summary>
    public bool EnableParanoidMode { get; set; } = true;

    /// <summary>Gets or sets whether to enable cryptographic commitment.</summary>
    public bool EnableCryptographicCommitment { get; set; } = true;

    /// <summary>Gets or sets whether to enable proof of storage.</summary>
    public bool EnableProofOfStorage { get; set; } = true;

    /// <summary>Gets or sets whether to enable Byzantine consensus.</summary>
    public bool EnableByzantineConsensus { get; set; } = true;

    /// <summary>Gets or sets whether to enable probabilistic verification.</summary>
    public bool EnableProbabilisticVerification { get; set; } = true;

    /// <summary>Gets or sets whether to enable entropy monitor.</summary>
    public bool EnableEntropyMonitor { get; set; } = true;

    /// <summary>Gets or sets whether to enable temporal consistency.</summary>
    public bool EnableTemporalConsistency { get; set; } = true;

    /// <summary>Gets or sets whether to enable fingerprint detection.</summary>
    public bool EnableFingerprintDetection { get; set; } = true;

    /// <summary>Gets or sets whether to enable honeypot.</summary>
    public bool EnableHoneypot { get; set; } = false; // Off by default

    /// <summary>Gets or sets whether to enable canary traps.</summary>
    public bool EnableCanaryTraps { get; set; } = false; // Off by default

    /// <summary>Gets or sets whether to enable asymmetric disclosure.</summary>
    public bool EnableAsymmetricDisclosure { get; set; } = true;

    /// <summary>
    /// Enable all security features.
    /// </summary>
    public void EnableAll()
    {
        EnableNetworkGuard = true;
        EnableViolationTracker = true;
        EnableConnectionFingerprint = true;
        EnablePeerReputation = true;
        EnableParanoidMode = true;
        EnableCryptographicCommitment = true;
        EnableProofOfStorage = true;
        EnableByzantineConsensus = true;
        EnableProbabilisticVerification = true;
        EnableEntropyMonitor = true;
        EnableTemporalConsistency = true;
        EnableFingerprintDetection = true;
        EnableHoneypot = true;
        EnableCanaryTraps = true;
        EnableAsymmetricDisclosure = true;
    }

    /// <summary>
    /// Disable all security features.
    /// </summary>
    public void DisableAll()
    {
        EnableNetworkGuard = false;
        EnableViolationTracker = false;
        EnableConnectionFingerprint = false;
        EnablePeerReputation = false;
        EnableParanoidMode = false;
        EnableCryptographicCommitment = false;
        EnableProofOfStorage = false;
        EnableByzantineConsensus = false;
        EnableProbabilisticVerification = false;
        EnableEntropyMonitor = false;
        EnableTemporalConsistency = false;
        EnableFingerprintDetection = false;
        EnableHoneypot = false;
        EnableCanaryTraps = false;
        EnableAsymmetricDisclosure = false;
    }
}

