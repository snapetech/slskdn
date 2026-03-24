// <copyright file="SecurityStartupTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Security;

using System.IO;
using System.Reflection;
using slskd.Tests.Unit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using slskd.Common.Security;
using Xunit;

[Collection("ProgramAppDirectory")]
public class SecurityStartupTests
{
    [Fact]
    public void AddSlskdnSecurity_NormalizesRelativeTransferSecurityPaths_AgainstAppDirectory()
    {
        var originalAppDirectory = Program.AppDirectory;
        var tempAppDirectory = Path.Combine(Path.GetTempPath(), "slskdn-security-startup-tests", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempAppDirectory);

        try
        {
            SetAppDirectory(tempAppDirectory);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["slskd:security:enabled"] = "true",
                    ["slskd:security:pathguard:downloadroot"] = "downloads-root",
                    ["slskd:security:pathguard:shareroot"] = "shares-root",
                    ["slskd:security:contentsafety:quarantinedirectory"] = "quarantine",
                    ["slskd:security:contentsafety:quarantinesuspicious"] = "true",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();

            services.AddSlskdnSecurity(configuration);

            using var provider = services.BuildServiceProvider();
            var transferSecurity = provider.GetRequiredService<TransferSecurity>();

            Assert.Equal(Path.Combine(tempAppDirectory, "downloads-root"), transferSecurity.DownloadRoot);
            Assert.Equal(Path.Combine(tempAppDirectory, "shares-root"), transferSecurity.ShareRoot);
            Assert.Equal(Path.Combine(tempAppDirectory, "quarantine"), transferSecurity.QuarantineDirectory);
            Assert.True(transferSecurity.QuarantineSuspicious);
        }
        finally
        {
            SetAppDirectory(originalAppDirectory);

            if (Directory.Exists(tempAppDirectory))
            {
                Directory.Delete(tempAppDirectory, true);
            }
        }
    }

    private static void SetAppDirectory(string? value)
    {
        var field = typeof(Program).GetField($"<{nameof(Program.AppDirectory)}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        field!.SetValue(null, value ?? string.Empty);
    }
}
