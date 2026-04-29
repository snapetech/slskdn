// <copyright file="YamlConfigurationSourceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Configuration;

using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using slskd;
using slskd.Configuration;
using Xunit;

public class YamlConfigurationSourceTests
{
    [Fact]
    public void AddYamlFile_BindsPublicDhtAliasToDhtRendezvousOptions()
    {
        var options = ReadOptions("""
            dht:
              enabled: true
              lan_only: true
              dht_port: 50406
            """);

        Assert.True(options.DhtRendezvous.Enabled);
        Assert.True(options.DhtRendezvous.LanOnly);
        Assert.Equal(50406, options.DhtRendezvous.DhtPort);
    }

    [Fact]
    public void AddYamlFile_StillBindsInternalDhtRendezvousKey()
    {
        var options = ReadOptions("""
            dhtRendezvous:
              enabled: false
            """);

        Assert.False(options.DhtRendezvous.Enabled);
    }

    private static Options ReadOptions(string yaml)
    {
        var directory = Path.Combine(Path.GetTempPath(), "slskd-yaml-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var path = Path.Combine(directory, "slskd.yml");
            File.WriteAllText(path, yaml);

            using var provider = new PhysicalFileProvider(directory);
            var configuration = new ConfigurationBuilder()
                .AddYamlFile(
                    path: Path.GetFileName(path),
                    targetType: typeof(Options),
                    optional: false,
                    provider: provider)
                .Build();

            var options = new Options();
            configuration.GetSection("slskd").Bind(options, o => { o.BindNonPublicProperties = true; });
            return options;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
