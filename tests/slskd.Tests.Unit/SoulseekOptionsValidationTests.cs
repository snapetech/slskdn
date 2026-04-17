// <copyright file="SoulseekOptionsValidationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit;

using System.ComponentModel.DataAnnotations;
using System.Linq;
using Xunit;

public class SoulseekOptionsValidationTests
{
    [Fact]
    public void Options_RejectsLoopbackSoulseekListenAddress_WhenConnecting()
    {
        var options = new Options
        {
            Soulseek = new Options.SoulseekOptions
            {
                ListenIpAddress = "127.0.0.1",
            },
            Flags = new Options.FlagsOptions
            {
                NoConnect = false,
            },
        };

        var results = options.Validate(new ValidationContext(options)).ToList();

        Assert.Contains(
            results,
            result => result.ErrorMessage!.Contains(
                "Soulseek.ListenIpAddress must not be a loopback address",
                System.StringComparison.Ordinal));
    }

    [Fact]
    public void Options_AllowsLoopbackSoulseekListenAddress_WhenNoConnectIsEnabled()
    {
        var options = new Options
        {
            Soulseek = new Options.SoulseekOptions
            {
                ListenIpAddress = "127.0.0.1",
            },
            Flags = new Options.FlagsOptions
            {
                NoConnect = true,
            },
        };

        var results = options.Validate(new ValidationContext(options)).ToList();

        Assert.DoesNotContain(
            results,
            result => result.ErrorMessage?.Contains(
                "Soulseek.ListenIpAddress must not be a loopback address",
                System.StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Options_RejectsMissingStableDhtPort_WhenDhtRendezvousIsEnabled()
    {
        var options = new Options
        {
            DhtRendezvous = new slskd.DhtRendezvous.DhtRendezvousOptions
            {
                Enabled = true,
                DhtPort = 0,
            },
        };

        var results = options.Validate(new ValidationContext(options)).ToList();

        Assert.Contains(
            results,
            result => result.ErrorMessage!.Contains(
                "DHT rendezvous requires an explicit UDP port",
                System.StringComparison.Ordinal));
    }

    [Fact]
    public void Options_RejectsMissingBootstrapRouters_WhenDhtRendezvousIsEnabled()
    {
        var options = new Options
        {
            DhtRendezvous = new slskd.DhtRendezvous.DhtRendezvousOptions
            {
                Enabled = true,
                DhtPort = 50306,
                BootstrapRouters = [],
            },
        };

        var results = options.Validate(new ValidationContext(options)).ToList();

        Assert.Contains(
            results,
            result => result.ErrorMessage!.Contains(
                "DHT rendezvous requires at least one bootstrap router",
                System.StringComparison.Ordinal));
    }

    [Fact]
    public void Options_AllowsStableDhtPortAndBootstrapRouters_WhenDhtRendezvousIsEnabled()
    {
        var options = new Options
        {
            DhtRendezvous = new slskd.DhtRendezvous.DhtRendezvousOptions
            {
                Enabled = true,
                DhtPort = 50306,
                BootstrapRouters =
                [
                    "router.bittorrent.com",
                    "router.utorrent.com",
                    "dht.transmissionbt.com",
                ],
            },
        };

        var results = options.Validate(new ValidationContext(options)).ToList();

        Assert.DoesNotContain(
            results,
            result => result.ErrorMessage?.Contains(
                "DHT rendezvous requires an explicit UDP port",
                System.StringComparison.Ordinal) == true);
        Assert.DoesNotContain(
            results,
            result => result.ErrorMessage?.Contains(
                "DHT rendezvous requires at least one bootstrap router",
                System.StringComparison.Ordinal) == true);
    }
}
