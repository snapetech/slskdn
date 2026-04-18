// <copyright file="SecurityStartup.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;

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
        // Note: Logging will be available after host build. For now, we'll proceed without debug logs.

        // Read from configuration sections
        // YAML provider normalizes keys to lowercase and prefixes with namespace (slskd:)
        // So "Security" in YAML becomes "slskd:security" in configuration
        var slskdSecuritySection = configuration.GetSection("slskd:security");
        var securitySection = configuration.GetSection(SecurityOptions.Section); // "Security"
        var securitySectionLower = configuration.GetSection("security");

        IConfigurationSection sectionToUse;
        if (slskdSecuritySection.Exists())
        {
            sectionToUse = slskdSecuritySection;
        }
        else if (securitySection.Exists())
        {
            sectionToUse = securitySection;
        }
        else if (securitySectionLower.Exists())
        {
            sectionToUse = securitySectionLower;
        }
        else
        {
            sectionToUse = securitySectionLower;
        }

        services.Configure<SecurityOptions>(sectionToUse);

        // CRITICAL FIX: Read the value directly from the bound Options object after configuration
        // The YAML provider may have issues with boolean parsing, so we bind to Options first
        // and then read from the bound object which should have the correct value
        var boundOptions = new slskd.Options();
        configuration.GetSection("slskd").Bind(boundOptions);

        var sectionGetResult = sectionToUse.Get<SecurityOptions>();
        var options = boundOptions.Security ?? sectionGetResult ?? new SecurityOptions();

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
        // Check if security is enabled before registering middleware
        // Read options directly from configuration to avoid timing issues with IOptions
        var configuration = app.ApplicationServices.GetService<IConfiguration>();
        Common.Security.SecurityOptions? options = null;
        var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("SecurityStartup.UseSlskdnSecurity");

        if (configuration != null)
        {
            logger?.LogInformation("[UseSlskdnSecurity] STEP 1: Checking configuration sections...");

            // YAML provider normalizes keys to lowercase and prefixes with namespace (slskd:)
            // Try paths in order of likelihood: slskd:security, Security, security
            var slskdSecuritySection = configuration.GetSection("slskd:security");
            var securitySection = configuration.GetSection(Common.Security.SecurityOptions.Section); // "Security"
            var securitySectionLower = configuration.GetSection("security");

            logger?.LogInformation("[UseSlskdnSecurity] STEP 1a: slskd:security.Exists() = {Exists}, enabled = {Enabled}",
                slskdSecuritySection.Exists(),
                slskdSecuritySection.Exists() ? slskdSecuritySection["enabled"] : "N/A");
            logger?.LogInformation("[UseSlskdnSecurity] STEP 1b: Security.Exists() = {Exists}, enabled = {Enabled}",
                securitySection.Exists(),
                securitySection.Exists() ? securitySection["enabled"] : "N/A");
            logger?.LogInformation("[UseSlskdnSecurity] STEP 1c: security.Exists() = {Exists}, enabled = {Enabled}",
                securitySectionLower.Exists(),
                securitySectionLower.Exists() ? securitySectionLower["enabled"] : "N/A");

            IConfigurationSection sectionToUse;
            if (slskdSecuritySection.Exists())
            {
                sectionToUse = slskdSecuritySection;
                logger?.LogInformation("[UseSlskdnSecurity] STEP 2: Using slskd:security section");
            }
            else if (securitySection.Exists())
            {
                sectionToUse = securitySection;
                logger?.LogInformation("[UseSlskdnSecurity] STEP 2: Using Security section");
            }
            else
            {
                sectionToUse = securitySectionLower;
                logger?.LogInformation("[UseSlskdnSecurity] STEP 2: Using security section (fallback)");
            }

            logger?.LogInformation("[UseSlskdnSecurity] STEP 3: Getting SecurityOptions from section...");
            options = sectionToUse.Get<Common.Security.SecurityOptions>();
            logger?.LogInformation("[UseSlskdnSecurity] STEP 3a: sectionToUse.Get result: Enabled = {Enabled}, Profile = {Profile}",
                options?.Enabled ?? (bool?)null,
                options?.Profile.ToString() ?? "null");
        }

        // Also try IOptionsMonitor as fallback
        if (options == null)
        {
            var optionsMonitor = app.ApplicationServices.GetService<IOptionsMonitor<Common.Security.SecurityOptions>>();
            if (optionsMonitor != null)
            {
                options = optionsMonitor.CurrentValue;
            }
        }

        // Fallback to IOptions if still null
        if (options == null)
        {
            var optionsWrapper = app.ApplicationServices.GetService<IOptions<Common.Security.SecurityOptions>>();
            options = optionsWrapper?.Value;
        }

        // CRITICAL: Always register the middleware, even when security is disabled
        // Path traversal protection should ALWAYS be enabled, even when other security features are disabled
        // The middleware will handle null services gracefully and still perform path traversal checks
        if (options != null && !options.Enabled)
        {
            // Security disabled - register middleware with null services
            // This ensures path traversal checks still run
            logger?.LogInformation("[UseSlskdnSecurity] Security is disabled (Enabled=false), but REGISTERING middleware for path traversal protection");
        }
        else
        {
            // Security enabled - register middleware with full services
            logger?.LogInformation("[UseSlskdnSecurity] Security is enabled (Enabled={Enabled}), REGISTERING middleware", options?.Enabled ?? true);
            if (options != null)
            {
                logger?.LogInformation("[UseSlskdnSecurity] Options: Enabled={Enabled}, Profile={Profile}", options.Enabled, options.Profile);
            }
        }

        // Always register middleware - it will use null services if security is disabled
        // but will still perform critical path traversal checks
        try
        {
            logger?.LogInformation("[UseSlskdnSecurity] About to call UseSecurityMiddleware...");
            app.UseSecurityMiddleware();
            logger?.LogInformation("[UseSlskdnSecurity] Security middleware registered successfully");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[UseSlskdnSecurity] ERROR registering security middleware: {Message}", ex.Message);
            throw;
        }

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
