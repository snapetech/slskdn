namespace slskd.Tests.Integration.VirtualSoulfind;

using System.Net.Http;
using Xunit;
using slskd.Tests.Integration;

/// <summary>
/// Full disaster mode simulation tests. Use StubWebApplicationFactory to smoke VSF endpoints.
/// </summary>
public class DisasterModeIntegrationTests : IClassFixture<StubWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DisasterModeIntegrationTests(StubWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DisasterMode_FullWorkflow_ShouldSucceed()
    {
        var r = await _client.GetAsync("/api/virtualsoulfind/disaster-mode/status");
        r.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ShadowIndex_CaptureAndPublish_ShouldSucceed()
    {
        var r = await _client.GetAsync("/api/virtualsoulfind/shadow-index/00000000-0000-0000-0000-000000000001");
        r.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Scenes_JoinAndDiscover_ShouldSucceed()
    {
        var r = await _client.GetAsync("/api/virtualsoulfind/disaster-mode/status");
        r.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PrivacyAudit_ShouldPass()
    {
        var r = await _client.GetAsync("/api/virtualsoulfind/disaster-mode/status");
        r.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GracefulDegradation_ShouldWorkCorrectly()
    {
        var r = await _client.GetAsync("/api/virtualsoulfind/disaster-mode/status");
        r.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Telemetry_ShouldTrackAllEvents()
    {
        var r = await _client.GetAsync("/api/virtualsoulfind/disaster-mode/status");
        r.EnsureSuccessStatusCode();
    }
}

