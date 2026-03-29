// <copyright file="MetricsOptionsValidationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Xunit;

/// <summary>
/// Validation tests for metrics configuration.
/// </summary>
public class MetricsOptionsValidationTests
{
    [Fact]
    public void Metrics_disabled_allows_empty_auth_password()
    {
        var options = new Options.MetricsOptions
        {
            Enabled = false,
            Authentication = new Options.MetricsOptions.MetricsAuthenticationOptions
            {
                Disabled = false,
                Username = "slskd",
                Password = string.Empty,
            },
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, true);

        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void Metrics_enabled_with_auth_requires_password()
    {
        var options = new Options.MetricsOptions
        {
            Enabled = true,
            Authentication = new Options.MetricsOptions.MetricsAuthenticationOptions
            {
                Disabled = false,
                Username = "slskd",
                Password = string.Empty,
            },
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, true);

        Assert.False(isValid);
        Assert.Contains(results, result => result.ErrorMessage!.Contains("Metrics authentication password must be configured", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Metrics_enabled_with_auth_disabled_allows_empty_password()
    {
        var options = new Options.MetricsOptions
        {
            Enabled = true,
            Authentication = new Options.MetricsOptions.MetricsAuthenticationOptions
            {
                Disabled = true,
                Username = string.Empty,
                Password = string.Empty,
            },
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, true);

        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void RelayAgentConfiguration_InvalidCidr_DoesNotLeakParserDetails()
    {
        var options = new Options.RelayOptions.RelayAgentConfigurationOptions
        {
            InstanceName = "agent",
            Secret = "0123456789abcdef",
            Cidr = "not-a-cidr",
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, true);

        Assert.False(isValid);
        var error = Assert.Single(results).ErrorMessage;
        Assert.Equal("CIDR not-a-cidr is invalid", error);
    }

    [Fact]
    public void BlacklistedOptions_InvalidCidr_DoesNotLeakParserDetails()
    {
        var options = new Options.GroupsOptions.BlacklistedOptions
        {
            Cidrs = new[] { "not-a-cidr" },
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, true);

        Assert.False(isValid);
        var error = Assert.Single(results).ErrorMessage;
        Assert.Equal("CIDR not-a-cidr is invalid", error);
    }

    [Fact]
    public void ApiKeyOptions_InvalidCidr_DoesNotLeakParserDetails()
    {
        var options = new Options.WebOptions.WebAuthenticationOptions.ApiKeyOptions
        {
            Key = "0123456789abcdef",
            Cidr = "not-a-cidr",
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, true);

        Assert.False(isValid);
        var error = Assert.Single(results).ErrorMessage;
        Assert.Equal("CIDR not-a-cidr is invalid", error);
    }
}
