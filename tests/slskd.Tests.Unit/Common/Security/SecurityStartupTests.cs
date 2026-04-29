// <copyright file="SecurityStartupTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Common.Security;

using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using slskd.Common.Security;
using Xunit;

public class SecurityStartupTests
{
    [Fact]
    public void AddSlskdnSecurity_BindsConfiguredSecurityOptions()
    {
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
        var options = provider.GetRequiredService<IOptions<SecurityOptions>>().Value;

        Assert.Equal("downloads-root", options.PathGuard.DownloadRoot);
        Assert.Equal("shares-root", options.PathGuard.ShareRoot);
        Assert.Equal("quarantine", options.ContentSafety.QuarantineDirectory);
        Assert.True(options.ContentSafety.QuarantineSuspicious);
    }
}
