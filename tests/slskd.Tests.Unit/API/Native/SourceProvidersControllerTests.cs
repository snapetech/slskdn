// <copyright file="SourceProvidersControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.API.Native;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using slskd.API.Native;
using slskd.VirtualSoulfind.Core;
using slskd.VirtualSoulfind.v2.Backends;
using Xunit;

public class SourceProvidersControllerTests
{
    [Fact]
    public void Get_ReturnsKnownProvidersWithRegistrationAndActiveState()
    {
        var controller = CreateController(
            enabled: true,
            ContentBackendType.LocalLibrary,
            ContentBackendType.Soulseek,
            ContentBackendType.Torrent);

        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SourceProviderCatalogResponse>(ok.Value);
        Assert.True(response.AcquisitionPlanningEnabled);
        Assert.NotEmpty(response.ProfilePolicies);

        var local = AssertProvider(response, "LocalLibrary");
        Assert.True(local.Registered);
        Assert.True(local.Active);
        Assert.Contains("download", local.Capabilities);

        var soulseek = AssertProvider(response, "Soulseek");
        Assert.True(soulseek.Registered);
        Assert.True(soulseek.Active);
        Assert.Equal("public-network", soulseek.RiskLevel);
        Assert.Contains("Rate-limited", soulseek.NetworkPolicy);

        var torrent = AssertProvider(response, "Torrent");
        Assert.True(torrent.Registered);
        Assert.False(torrent.Active);
        Assert.True(torrent.RequiresConfiguration);
        Assert.Equal("high-risk", torrent.RiskLevel);
        Assert.Equal("Disabled by default.", torrent.DisabledReason);
    }

    [Fact]
    public void Get_ReturnsProfileProviderPoliciesWithAutomationDisabled()
    {
        var controller = CreateController(
            enabled: true,
            ContentBackendType.LocalLibrary,
            ContentBackendType.Soulseek,
            ContentBackendType.NativeMesh,
            ContentBackendType.MeshDht);

        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SourceProviderCatalogResponse>(ok.Value);
        var meshPreferred = Assert.Single(response.ProfilePolicies.Where(policy => policy.ProfileId == "mesh-preferred"));
        Assert.Equal(new[] { "LocalLibrary", "NativeMesh", "MeshDht", "Soulseek" }, meshPreferred.ProviderPriority);
        Assert.False(meshPreferred.AutoDownloadEnabled);
        Assert.All(response.ProfilePolicies, policy => Assert.False(policy.AutoDownloadEnabled));
    }

    [Fact]
    public void Get_WhenAcquisitionPlanningDisabled_ReturnsVisibleDisabledProviders()
    {
        var controller = CreateController(
            enabled: false,
            ContentBackendType.LocalLibrary,
            ContentBackendType.Soulseek);

        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SourceProviderCatalogResponse>(ok.Value);
        Assert.False(response.AcquisitionPlanningEnabled);
        Assert.All(response.Providers, provider => Assert.False(provider.Active));
        Assert.All(response.Providers, provider => Assert.Equal("VirtualSoulfind v2 acquisition planning is disabled.", provider.DisabledReason));
    }

    [Fact]
    public void Get_WhenProviderNotRegistered_ShowsRegistrationReason()
    {
        var controller = CreateController(enabled: true, ContentBackendType.LocalLibrary);

        var result = controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SourceProviderCatalogResponse>(ok.Value);
        var soulseek = AssertProvider(response, "Soulseek");
        Assert.False(soulseek.Registered);
        Assert.False(soulseek.Active);
        Assert.Equal("Provider service is not registered in this build.", soulseek.DisabledReason);
    }

    private static SourceProvidersController CreateController(bool enabled, params ContentBackendType[] registeredTypes)
    {
        var backends = registeredTypes.Select(type =>
        {
            var backend = new Mock<IContentBackend>();
            backend.SetupGet(item => item.Type).Returns(type);
            backend.SetupGet(item => item.SupportedDomain).Returns(ContentDomain.Music);
            return backend.Object;
        });

        var options = new Mock<IOptionsSnapshot<slskd.Options>>();
        options
            .SetupGet(snapshot => snapshot.Value)
            .Returns(new slskd.Options
            {
                VirtualSoulfindV2 = new slskd.VirtualSoulfind.v2.Configuration.VirtualSoulfindOptions
                {
                    Enabled = enabled,
                },
            });

        return new SourceProvidersController(backends, options.Object);
    }

    private static SourceProviderResponse AssertProvider(SourceProviderCatalogResponse response, string id)
    {
        return Assert.Single(response.Providers.Where(provider => provider.Id == id));
    }
}
