// <copyright file="SoulseekOptionsValidationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit;

using System.Collections.Generic;
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
}
