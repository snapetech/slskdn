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

    [Fact]
    public void AddYamlFile_BindsTransfersAliasToGlobalOptions()
    {
        var options = ReadOptions("""
            transfers:
              upload:
                slots: 3
              download:
                retry:
                  attempts: 4
            """);

        Assert.Equal(3, options.Global.Upload.Slots);
        Assert.Equal(4, options.Global.Download.Retry.Attempts);
    }

    [Fact]
    public void AddYamlFile_BindsIntegrationsAliasToIntegrationOptions()
    {
        var options = ReadOptions("""
            integrations:
              musicBrainz:
                enabled: true
                base_url: https://musicbrainz.example.invalid
            """);

        Assert.True(options.Integration.MusicBrainz.Enabled);
        Assert.Equal("https://musicbrainz.example.invalid", options.Integration.MusicBrainz.BaseUrl);
    }

    [Fact]
    public void AddYamlFile_BindsGroupLimitsNestedUnderUpload()
    {
        var options = ReadOptions("""
            groups:
              default:
                upload:
                  limits:
                    queued:
                      files: 2
              user_defined:
                friends:
                  upload:
                    limits:
                      daily:
                        megabytes: 512
            """);

        Assert.Equal(2, options.Groups.Default.Limits.Queued.Files);
        Assert.Equal(512, options.Groups.UserDefined["friends"].Limits.Daily.Megabytes);
    }

    [Fact]
    public void Reload_RemovesStaleSequenceKeys()
    {
        var directory = Path.Combine(Path.GetTempPath(), "slskd-yaml-reload-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var path = Path.Combine(directory, "slskd.yml");
            File.WriteAllText(path, """
                rooms:
                  - alpha
                  - beta
                """);

            using var provider = new PhysicalFileProvider(directory);
            var configuration = new ConfigurationBuilder()
                .AddYamlFile(
                    path: Path.GetFileName(path),
                    targetType: typeof(Options),
                    optional: false,
                    provider: provider)
                .Build();

            var initial = new Options();
            configuration.GetSection("slskd").Bind(initial, o => { o.BindNonPublicProperties = true; });
            Assert.Equal(new[] { "alpha", "beta" }, initial.Rooms);

            File.WriteAllText(path, """
                rooms:
                  - gamma
                """);

            ((IConfigurationRoot)configuration).Reload();

            var reloaded = new Options();
            configuration.GetSection("slskd").Bind(reloaded, o => { o.BindNonPublicProperties = true; });
            Assert.Equal(new[] { "gamma" }, reloaded.Rooms);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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
