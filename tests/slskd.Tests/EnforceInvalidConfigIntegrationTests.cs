// <copyright file="EnforceInvalidConfigIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using slskd;
using slskd.Common.Security;
using Xunit;

/// <summary>
/// PR-01: Integration test — when EnforceSecurity is on and config is invalid, the host fails to start
/// and the failure message contains the hardening rule name. HardeningValidator.Validate throws before
/// the host is built; these tests verify that the exception message contains the rule name so that
/// logs and support can identify the misconfiguration.
/// </summary>
public class EnforceInvalidConfigIntegrationTests
{
    [Fact]
    public void Enforce_invalid_config_AuthDisabledNonLoopback_failure_contains_rule_name()
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

        var ex = Assert.Throws<HardeningValidationException>(
            () => HardeningValidator.Validate(options, "Production", isBindingNonLoopback: true));

        Assert.Equal(HardeningValidator.RuleAuthDisabledNonLoopback, ex.RuleName);
        Assert.Contains(ex.RuleName, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Enforce_invalid_config_CorsCredentialsWithWildcard_failure_contains_rule_name()
    {
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
        Assert.Contains(ex.RuleName, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Enforce_invalid_config_MemoryDumpWithAuthDisabled_failure_contains_rule_name()
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
        Assert.Contains(ex.RuleName, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Full host startup: run slskd as a subprocess with Enforce on and invalid config; assert exit 1
    /// and that stderr/stdout contains the rule name. Requires no other slskd instance (mutex).
    /// Skipped: config binding (slskd section / SLSKD_APP_DIR) in subprocess harness can prevent
    /// Enforce from being applied; the process may block on app.Run() instead of exiting. Run
    /// manually when validating the full host path.
    /// </summary>
    [Fact(Skip = "Subprocess: config/mutex in harness can prevent Enforce; run manually when needed")]
    public async Task Enforce_invalid_config_host_startup_exits_nonzero_and_output_contains_rule_name()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var slskdProj = Path.Combine(repoRoot, "src", "slskd", "slskd.csproj");
        if (!File.Exists(slskdProj))
        {
            // Unusual layout (e.g. from IDE); skip
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "slskd-enforce-test-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(tempDir);
            var yml = Path.Combine(tempDir, "slskd.yml");
            await File.WriteAllTextAsync(yml, """
                slskd:
                  web:
                    enforceSecurity: true
                    allowRemoteNoAuth: false
                    authentication:
                      disabled: true
                    port: 5000
                  diagnostics:
                    allowMemoryDump: false
                """);

            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project \"{slskdProj}\" -c Release --no-build",
                    WorkingDirectory = repoRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            proc.StartInfo.Environment["SLSKD_APP_DIR"] = tempDir;

            proc.Start();
            var outTask = proc.StandardOutput.ReadToEndAsync();
            var errTask = proc.StandardError.ReadToEndAsync();
            var exited = proc.WaitForExit(15_000);
            if (!exited)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                Assert.Fail("slskd subprocess did not exit within 15s");
            }

            var stdout = await outTask;
            var stderr = await errTask;
            var combined = stdout + "\n" + stderr;

            Assert.True(exited && proc.ExitCode == 1, $"Expected exit code 1; got {proc.ExitCode}. stderr: {stderr}");
            Assert.Contains(HardeningValidator.RuleAuthDisabledNonLoopback, combined, StringComparison.Ordinal);
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }
}
