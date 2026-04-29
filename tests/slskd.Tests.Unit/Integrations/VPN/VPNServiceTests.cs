// <copyright file="VPNServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Integrations.VPN;

using System;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Moq;
using slskd.Integrations.VPN;
using Soulseek;
using Xunit;

public class VPNServiceTests
{
    [Fact]
    public async Task PollingObserver_WhenVpnClientIsMisconfigured_SwallowsTimerCallbackException()
    {
        var optionsAtStartup = new OptionsAtStartup
        {
            Integration = new OptionsAtStartup.IntegrationOptions
            {
                Vpn = new OptionsAtStartup.IntegrationOptions.VpnOptions
                {
                    Enabled = true,
                    PollingInterval = 100,
                },
            },
        };

        var service = new VPNService(
            optionsAtStartup,
            new TestOptionsMonitor<Options>(new Options()),
            Mock.Of<IStateMutator<State>>(),
            Mock.Of<ISoulseekClient>(),
            Mock.Of<IHttpClientFactory>());

        var observerMethod = typeof(VPNService).GetMethod(
            "ObserveCheckConnectionAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("VPNService.ObserveCheckConnectionAsync method was not found.");

        var task = (Task?)observerMethod.Invoke(service, null)
            ?? throw new InvalidOperationException("VPNService.ObserveCheckConnectionAsync did not return a task.");

        await task;

        Assert.False(service.IsReady);
        Assert.False(service.Status.IsConnected);
    }
}
