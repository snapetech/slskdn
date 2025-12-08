// <copyright file="SecurityStartup.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Extension methods for configuring security in Program.cs.
/// Provides a clean, single entry point for security setup.
/// </summary>
public static class SecurityStartup
{
    /// <summary>
    /// Add security services to the service collection.
    /// Call this in ConfigureServices / builder.Services.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddSlskdnSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind options from configuration
        var securitySection = configuration.GetSection(SecurityOptions.Section);
        services.Configure<SecurityOptions>(securitySection);

        var options = securitySection.Get<SecurityOptions>() ?? new SecurityOptions();

        if (!options.Enabled)
        {
            // Security disabled - register null implementations or skip
            services.AddSingleton<SecurityServices>(sp => new SecurityServices());
            return services;
        }

        // Determine which features to enable based on profile
        var registrationOptions = GetRegistrationOptions(options);

        // Register core security services
        services.AddSecurityServices(opt =>
        {
            if (registrationOptions.EnableNetworkGuard) opt.EnableNetworkGuard = true;
            if (registrationOptions.EnableViolationTracker) opt.EnableViolationTracker = true;
            if (registrationOptions.EnableConnectionFingerprint) opt.EnableConnectionFingerprint = true;
            if (registrationOptions.EnablePeerReputation) opt.EnablePeerReputation = true;
            if (registrationOptions.EnableParanoidMode) opt.EnableParanoidMode = true;
            if (registrationOptions.EnableCryptographicCommitment) opt.EnableCryptographicCommitment = true;
            if (registrationOptions.EnableProofOfStorage) opt.EnableProofOfStorage = true;
            if (registrationOptions.EnableByzantineConsensus) opt.EnableByzantineConsensus = true;
            if (registrationOptions.EnableProbabilisticVerification) opt.EnableProbabilisticVerification = true;
            if (registrationOptions.EnableEntropyMonitor) opt.EnableEntropyMonitor = true;
            if (registrationOptions.EnableTemporalConsistency) opt.EnableTemporalConsistency = true;
            if (registrationOptions.EnableFingerprintDetection) opt.EnableFingerprintDetection = true;
            if (registrationOptions.EnableHoneypot) opt.EnableHoneypot = true;
            if (registrationOptions.EnableCanaryTraps) opt.EnableCanaryTraps = true;
            if (registrationOptions.EnableAsymmetricDisclosure) opt.EnableAsymmetricDisclosure = true;
        });

        // Register SecurityServices aggregate
        services.AddSingleton<SecurityServices>(sp => new SecurityServices
        {
            NetworkGuard = sp.GetService<NetworkGuard>(),
            ViolationTracker = sp.GetService<ViolationTracker>(),
            ConnectionFingerprint = sp.GetService<ConnectionFingerprint>(),
            PeerReputation = sp.GetService<PeerReputation>(),
            ParanoidMode = sp.GetService<ParanoidMode>(),
            CryptographicCommitment = sp.GetService<CryptographicCommitment>(),
            ProofOfStorage = sp.GetService<ProofOfStorage>(),
            ByzantineConsensus = sp.GetService<ByzantineConsensus>(),
            ProbabilisticVerification = sp.GetService<ProbabilisticVerification>(),
            EntropyMonitor = sp.GetService<EntropyMonitor>(),
            TemporalConsistency = sp.GetService<TemporalConsistency>(),
            FingerprintDetection = sp.GetService<FingerprintDetection>(),
            Honeypot = sp.GetService<Honeypot>(),
            CanaryTraps = sp.GetService<CanaryTraps>(),
            AsymmetricDisclosure = sp.GetService<AsymmetricDisclosure>(),
            EventSink = sp.GetService<ISecurityEventSink>(),
        });

        // Register TransferSecurity for file transfer integration
        services.AddSingleton<TransferSecurity>(sp =>
        {
            var transferSecurity = new TransferSecurity(
                sp.GetRequiredService<ILogger<TransferSecurity>>(),
                sp.GetService<ISecurityEventSink>(),
                sp.GetService<ViolationTracker>(),
                sp.GetService<PeerReputation>(),
                sp.GetService<TemporalConsistency>(),
                sp.GetService<Honeypot>());

            // Apply path configuration
            if (!string.IsNullOrEmpty(options.PathGuard.DownloadRoot))
            {
                transferSecurity.DownloadRoot = options.PathGuard.DownloadRoot;
            }

            if (!string.IsNullOrEmpty(options.PathGuard.ShareRoot))
            {
                transferSecurity.ShareRoot = options.PathGuard.ShareRoot;
            }

            if (!string.IsNullOrEmpty(options.ContentSafety.QuarantineDirectory))
            {
                transferSecurity.QuarantineDirectory = options.ContentSafety.QuarantineDirectory;
                transferSecurity.QuarantineSuspicious = options.ContentSafety.QuarantineSuspicious;
            }

            return transferSecurity;
        });

        return services;
    }

    /// <summary>
    /// Use security middleware in the application pipeline.
    /// Call this in Configure / app.Use*.
    /// Should be called early, before authentication.
    /// </summary>
    /// <param name="app">Application builder.</param>
    /// <returns>Application builder for chaining.</returns>
    public static IApplicationBuilder UseSlskdnSecurity(this IApplicationBuilder app)
    {
        // Add security middleware
        app.UseSecurityMiddleware();

        return app;
    }

    /// <summary>
    /// Map security API endpoints.
    /// Call this after UseRouting and UseAuthentication.
    /// </summary>
    /// <param name="app">Web application.</param>
    /// <returns>Web application for chaining.</returns>
    public static WebApplication MapSlskdnSecurityEndpoints(this WebApplication app)
    {
        // The SecurityController is automatically mapped via [ApiController]
        // This method is for any additional manual endpoint mapping if needed
        return app;
    }

    private static SecurityServiceRegistrationOptions GetRegistrationOptions(SecurityOptions options)
    {
        var reg = new SecurityServiceRegistrationOptions();

        switch (options.Profile)
        {
            case SecurityProfile.Minimal:
                // Only critical protections
                reg.EnableNetworkGuard = options.NetworkGuard.Enabled;
                reg.EnableViolationTracker = options.ViolationTracker.Enabled;
                reg.EnablePeerReputation = false;
                reg.EnableParanoidMode = false;
                reg.EnableCryptographicCommitment = false;
                reg.EnableProofOfStorage = false;
                reg.EnableByzantineConsensus = false;
                reg.EnableProbabilisticVerification = false;
                reg.EnableEntropyMonitor = false;
                reg.EnableTemporalConsistency = false;
                reg.EnableFingerprintDetection = false;
                reg.EnableHoneypot = false;
                reg.EnableCanaryTraps = false;
                reg.EnableAsymmetricDisclosure = false;
                reg.EnableConnectionFingerprint = false;
                break;

            case SecurityProfile.Standard:
                // Balanced protection (default)
                reg.EnableNetworkGuard = options.NetworkGuard.Enabled;
                reg.EnableViolationTracker = options.ViolationTracker.Enabled;
                reg.EnableConnectionFingerprint = true;
                reg.EnablePeerReputation = options.PeerReputation.Enabled;
                reg.EnableParanoidMode = false;
                reg.EnableCryptographicCommitment = true;
                reg.EnableProofOfStorage = true;
                reg.EnableByzantineConsensus = true;
                reg.EnableProbabilisticVerification = true;
                reg.EnableEntropyMonitor = true;
                reg.EnableTemporalConsistency = true;
                reg.EnableFingerprintDetection = true;
                reg.EnableHoneypot = false;
                reg.EnableCanaryTraps = false;
                reg.EnableAsymmetricDisclosure = true;
                break;

            case SecurityProfile.Maximum:
                // All features
                reg.EnableAll();
                break;

            case SecurityProfile.Custom:
            default:
                // Use individual settings
                reg.EnableNetworkGuard = options.NetworkGuard.Enabled;
                reg.EnableViolationTracker = options.ViolationTracker.Enabled;
                reg.EnableConnectionFingerprint = true;
                reg.EnablePeerReputation = options.PeerReputation.Enabled;
                reg.EnableParanoidMode = options.ParanoidMode.Enabled;
                reg.EnableCryptographicCommitment = true;
                reg.EnableProofOfStorage = true;
                reg.EnableByzantineConsensus = true;
                reg.EnableProbabilisticVerification = true;
                reg.EnableEntropyMonitor = true;
                reg.EnableTemporalConsistency = true;
                reg.EnableFingerprintDetection = true;
                reg.EnableHoneypot = options.Honeypot.Enabled;
                reg.EnableCanaryTraps = options.Honeypot.Enabled;
                reg.EnableAsymmetricDisclosure = true;
                break;
        }

        return reg;
    }
}

