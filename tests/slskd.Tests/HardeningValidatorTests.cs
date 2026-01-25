// <copyright file="HardeningValidatorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using System;
using slskd;
using slskd.Common.Security;
using Xunit;

/// <summary>
/// Unit tests for <see cref="HardeningValidator"/>. When EnforceSecurity is on, dangerous configs must throw
/// <see cref="HardeningValidationException"/> with the expected rule name. When EnforceSecurity is off, no exception.
/// </summary>
public class HardeningValidatorTests
{
    [Fact]
    public void EnforceSecurity_false_does_not_throw_even_with_bad_combo()
    {
        var options = new OptionsAtStartup
        {
            Web = new Options.WebOptions
            {
                EnforceSecurity = false,
                AllowRemoteNoAuth = false,
                Authentication = new Options.WebOptions.WebAuthenticationOptions { Disabled = true },
            },
        };

        HardeningValidator.Validate(options, "Production", isBindingNonLoopback: true);
    }

    [Fact]
    public void EnforceSecurity_true_AuthDisabled_NonLoopback_AllowRemoteNoAuth_false_throws_AuthDisabledNonLoopback()
    {
        var options = new OptionsAtStartup
        {
            Web = new Options.WebOptions
            {
                EnforceSecurity = true,
                AllowRemoteNoAuth = false,
                Authentication = new Options.WebOptions.WebAuthenticationOptions { Disabled = true },
                Cors = new Options.WebOptions.CorsOptions { Enabled = true, AllowCredentials = false }, // avoid CORS rule
            },
            Diagnostics = new Options.DiagnosticsOptions { AllowMemoryDump = false },
        };

        var ex = Assert.Throws<HardeningValidationException>(
            () => HardeningValidator.Validate(options, "Production", isBindingNonLoopback: true));

        Assert.Equal(HardeningValidator.RuleAuthDisabledNonLoopback, ex.RuleName);
        Assert.Contains("Authentication is disabled", ex.Message);
    }

    [Fact]
    public void EnforceSecurity_true_Cors_Disabled_does_not_throw()
    {
        // PR-04: when Cors.Enabled=false, no CORS middleware; CORS rule is skipped
        var options = new OptionsAtStartup
        {
            Web = new Options.WebOptions
            {
                EnforceSecurity = true,
                AllowRemoteNoAuth = true,
                Authentication = new Options.WebOptions.WebAuthenticationOptions { Disabled = false },
                Cors = new Options.WebOptions.CorsOptions { Enabled = false },
            },
            Diagnostics = new Options.DiagnosticsOptions { AllowMemoryDump = false },
        };

        HardeningValidator.Validate(options, "Production", isBindingNonLoopback: true);
    }

    [Fact]
    public void EnforceSecurity_true_CorsCredentialsWithWildcard_explicit_throws_CorsCredentialsWithWildcard()
    {
        // Explicit: Cors.Enabled=true, AllowCredentials=true, AllowedOrigins empty (=any)
        var options = new OptionsAtStartup
        {
            Web = new Options.WebOptions
            {
                EnforceSecurity = true,
                AllowRemoteNoAuth = true,
                Authentication = new Options.WebOptions.WebAuthenticationOptions { Disabled = false },
                Cors = new Options.WebOptions.CorsOptions
                {
                    Enabled = true,
                    AllowCredentials = true,
                    AllowedOrigins = Array.Empty<string>(),
                },
            },
            Diagnostics = new Options.DiagnosticsOptions { AllowMemoryDump = false },
        };

        var ex = Assert.Throws<HardeningValidationException>(
            () => HardeningValidator.Validate(options, "Production", isBindingNonLoopback: true));

        Assert.Equal(HardeningValidator.RuleCorsCredentialsWithWildcard, ex.RuleName);
    }

    [Fact]
    public void EnforceSecurity_true_MemoryDump_AuthDisabled_throws_MemoryDumpWithAuthDisabled()
    {
        var options = new OptionsAtStartup
        {
            Web = new Options.WebOptions
            {
                EnforceSecurity = true,
                AllowRemoteNoAuth = true,
                Authentication = new Options.WebOptions.WebAuthenticationOptions { Disabled = true },
                Cors = new Options.WebOptions.CorsOptions { Enabled = true, AllowCredentials = false },
            },
            Diagnostics = new Options.DiagnosticsOptions { AllowMemoryDump = true },
        };

        var ex = Assert.Throws<HardeningValidationException>(
            () => HardeningValidator.Validate(options, "Production", isBindingNonLoopback: true));

        Assert.Equal(HardeningValidator.RuleMemoryDumpWithAuthDisabled, ex.RuleName);
        Assert.Contains("AllowMemoryDump", ex.Message);
    }

    /// <summary>
    /// §11: Flags.HashFromAudioFileEnabled when EnforceSecurity must fail startup.
    /// </summary>
    [Fact]
    public void EnforceSecurity_true_HashFromAudioFileEnabled_throws_HashFromAudioFileEnabled()
    {
        var options = new OptionsAtStartup
        {
            Web = new Options.WebOptions
            {
                EnforceSecurity = true,
                AllowRemoteNoAuth = true,
                Authentication = new Options.WebOptions.WebAuthenticationOptions { Disabled = false },
                Cors = new Options.WebOptions.CorsOptions { Enabled = false },
            },
            Diagnostics = new Options.DiagnosticsOptions { AllowMemoryDump = false },
            Flags = new Options.FlagsOptions { HashFromAudioFileEnabled = true },
        };

        var ex = Assert.Throws<HardeningValidationException>(
            () => HardeningValidator.Validate(options, "Production", isBindingNonLoopback: true));

        Assert.Equal(HardeningValidator.RuleHashFromAudioFileEnabled, ex.RuleName);
        Assert.Contains("HashFromAudioFileEnabled", ex.Message);
    }

    [Fact]
    public void EnforceSecurity_true_valid_config_does_not_throw()
    {
        // Enforce on, auth on, CORS with Cors.Enabled=true and AllowCredentials=false (no cred+any), no memory dump
        var options = new OptionsAtStartup
        {
            Web = new Options.WebOptions
            {
                EnforceSecurity = true,
                AllowRemoteNoAuth = false,
                Authentication = new Options.WebOptions.WebAuthenticationOptions { Disabled = false },
                Cors = new Options.WebOptions.CorsOptions { Enabled = true, AllowCredentials = false },
            },
            Diagnostics = new Options.DiagnosticsOptions { AllowMemoryDump = false },
        };

        HardeningValidator.Validate(options, "Production", isBindingNonLoopback: true);
    }

    [Fact]
    public void EnforceSecurity_true_loopback_bind_AuthDisabled_does_not_throw()
    {
        var options = new OptionsAtStartup
        {
            Web = new Options.WebOptions
            {
                EnforceSecurity = true,
                AllowRemoteNoAuth = false,
                Authentication = new Options.WebOptions.WebAuthenticationOptions { Disabled = true },
                Cors = new Options.WebOptions.CorsOptions { Enabled = true, AllowCredentials = false },
            },
            Diagnostics = new Options.DiagnosticsOptions { AllowMemoryDump = false },
        };

        HardeningValidator.Validate(options, "Production", isBindingNonLoopback: false);
    }

    [Fact]
    public void Options_null_does_not_throw()
    {
        HardeningValidator.Validate(null!, "Production", isBindingNonLoopback: true);
    }
}
