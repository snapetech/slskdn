// <copyright file="TwoNodeMeshFullInstanceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Integration.DhtRendezvous;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Common.Security.API;
using slskd.DhtRendezvous.API;
using slskd.DhtRendezvous.Security;
using slskd.Mesh;
using slskd.Shares;
using slskd.Tests.Integration.Harness;
using Xunit;

[Trait("Category", "L2-Integration")]
[Trait("Category", "DhtRendezvous")]
[Trait("Category", "FullInstance")]
public class TwoNodeMeshFullInstanceTests
{
    [Fact]
    public async Task TwoFullInstances_CanFormOverlayMeshConnection()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        await using var alpha = new SlskdnFullInstanceRunner(
            loggerFactory.CreateLogger<SlskdnFullInstanceRunner>(),
            $"mesh-alpha-{Guid.NewGuid():N}"[..19]);
        await using var beta = new SlskdnFullInstanceRunner(
            loggerFactory.CreateLogger<SlskdnFullInstanceRunner>(),
            $"mesh-beta-{Guid.NewGuid():N}"[..18]);

        await alpha.StartAsync(
            disableAuthentication: true,
            soulseekUsername: "mesh-alpha",
            soulseekPassword: "mesh-alpha-pass");
        await beta.StartAsync(
            disableAuthentication: true,
            soulseekUsername: "mesh-beta",
            soulseekPassword: "mesh-beta-pass");

        Assert.True(alpha.OverlayPort.HasValue);
        Assert.True(beta.OverlayPort.HasValue);

        using var alphaClient = new HttpClient { BaseAddress = new Uri(alpha.ApiUrl) };
        using var betaClient = new HttpClient { BaseAddress = new Uri(beta.ApiUrl) };
        alphaClient.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", "integration-test");
        betaClient.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", "integration-test");

        var connectResponse = await alphaClient.PostAsJsonAsync(
            "/api/v0/overlay/connect?api_key=integration-test",
            new ConnectOverlayPeerRequest
            {
                Address = "127.0.0.1",
                Port = beta.OverlayPort.Value,
            });

        connectResponse.EnsureSuccessStatusCode();
        var connectBody = await connectResponse.Content.ReadFromJsonAsync<OverlayConnectResultResponse>();
        Assert.NotNull(connectBody);
        Assert.True(connectBody!.Connected);
        Assert.Equal(beta.OverlayPort.Value, connectBody.Port);

        string? overlayFailureDetails = null;
        await WaitForAsync(
            async () =>
            {
                var alphaConnections = await alphaClient.GetFromJsonAsync<List<MeshPeerInfoResponse>>("/api/v0/overlay/connections");
                var betaConnections = await betaClient.GetFromJsonAsync<List<MeshPeerInfoResponse>>("/api/v0/overlay/connections");

                var alphaConnected = alphaConnections?.Exists(peer =>
                    string.Equals(peer.Username, "mesh-beta", StringComparison.OrdinalIgnoreCase)) == true;
                var betaConnected = betaConnections?.Exists(peer =>
                    string.Equals(peer.Username, "mesh-alpha", StringComparison.OrdinalIgnoreCase)) == true;

                overlayFailureDetails = await BuildFailureDetailsAsync(alphaClient, betaClient, alphaConnections, betaConnections);
                return alphaConnected && betaConnected;
            },
            TimeSpan.FromSeconds(20),
            () => "full-instance overlay mesh neighbors did not appear on both nodes\n" + overlayFailureDetails);

        string? peerStatsFailureDetails = null;
        await WaitForAsync(
            async () =>
            {
                var alphaPeerStats = await alphaClient.GetFromJsonAsync<PeerStatistics>("/api/v0/security/peers/stats");
                var betaPeerStats = await betaClient.GetFromJsonAsync<PeerStatistics>("/api/v0/security/peers/stats");
                peerStatsFailureDetails = await BuildFailureDetailsAsync(alphaClient, betaClient, alphaPeerStats: alphaPeerStats, betaPeerStats: betaPeerStats);
                return alphaPeerStats is { TotalPeers: >= 1, OnionRoutingPeers: >= 1 }
                    && betaPeerStats is { TotalPeers: >= 1, OnionRoutingPeers: >= 1 };
            },
            TimeSpan.FromSeconds(20),
            () => "mesh peer inventory did not reflect the full-instance overlay connection\n" + peerStatsFailureDetails);

        await Task.Delay(OverlayTimeouts.MessageRead + TimeSpan.FromSeconds(5));

        string? idleFailureDetails = null;
        await WaitForAsync(
            async () =>
            {
                var alphaConnections = await alphaClient.GetFromJsonAsync<List<MeshPeerInfoResponse>>("/api/v0/overlay/connections");
                var betaConnections = await betaClient.GetFromJsonAsync<List<MeshPeerInfoResponse>>("/api/v0/overlay/connections");

                var alphaStillConnected = alphaConnections?.Exists(peer =>
                    string.Equals(peer.Username, "mesh-beta", StringComparison.OrdinalIgnoreCase)) == true;
                var betaStillConnected = betaConnections?.Exists(peer =>
                    string.Equals(peer.Username, "mesh-alpha", StringComparison.OrdinalIgnoreCase)) == true;

                idleFailureDetails = await BuildFailureDetailsAsync(alphaClient, betaClient, alphaConnections, betaConnections);
                return alphaStillConnected && betaStillConnected;
            },
            TimeSpan.FromSeconds(5),
            () => "overlay mesh neighbors disconnected after one message-read timeout\n" + idleFailureDetails);
    }

    [Fact]
    public async Task TwoFullInstances_CanSearchAndDownloadOverOverlayMesh()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        await using var alpha = new SlskdnFullInstanceRunner(
            loggerFactory.CreateLogger<SlskdnFullInstanceRunner>(),
            $"mesh-alpha-{Guid.NewGuid():N}"[..19]);
        await using var beta = new SlskdnFullInstanceRunner(
            loggerFactory.CreateLogger<SlskdnFullInstanceRunner>(),
            $"mesh-beta-{Guid.NewGuid():N}"[..18]);

        var probeId = Guid.NewGuid().ToString("N")[..12];
        var probeFilename = $"meshprobe{probeId}.flac";
        var probeBytes = Enumerable.Range(0, 8192).Select(i => (byte)(i % 251)).ToArray();
        var betaSharePath = Path.Combine(beta.SharesDirectory, probeFilename);
        await File.WriteAllBytesAsync(betaSharePath, probeBytes);

        await alpha.StartAsync(
            disableAuthentication: true,
            soulseekUsername: "mesh-alpha",
            soulseekPassword: "mesh-alpha-pass");
        await beta.StartAsync(
            disableAuthentication: true,
            soulseekUsername: "mesh-beta",
            soulseekPassword: "mesh-beta-pass");

        Assert.True(beta.OverlayPort.HasValue);

        using var alphaClient = new HttpClient { BaseAddress = new Uri(alpha.ApiUrl) };
        using var betaClient = new HttpClient { BaseAddress = new Uri(beta.ApiUrl) };

        var scanResponse = await betaClient.PutAsync("/api/v0/shares?api_key=integration-test", content: null);
        scanResponse.EnsureSuccessStatusCode();

        var contentId = $"content:test:{probeId}";
        await WaitForAsync(
            () => TrySeedContentItemAsync(beta, probeFilename, contentId),
            TimeSpan.FromSeconds(20),
            () => "beta share repository did not index the probe file");

        var connectResponse = await alphaClient.PostAsJsonAsync(
            "/api/v0/overlay/connect?api_key=integration-test",
            new ConnectOverlayPeerRequest
            {
                Address = "127.0.0.1",
                Port = beta.OverlayPort.Value,
            });

        connectResponse.EnsureSuccessStatusCode();

        await WaitForAsync(
            async () =>
            {
                var alphaConnections = await alphaClient.GetFromJsonAsync<List<MeshPeerInfoResponse>>("/api/v0/overlay/connections");
                return alphaConnections?.Exists(peer =>
                    string.Equals(peer.Username, "mesh-beta", StringComparison.OrdinalIgnoreCase)) == true;
            },
            TimeSpan.FromSeconds(20),
            () => "alpha did not keep an outbound overlay connection to beta");

        var searchResponse = await alphaClient.PostAsJsonAsync(
            "/api/v0/searches?api_key=integration-test",
            new
            {
                searchText = Path.GetFileNameWithoutExtension(probeFilename),
                filterResponses = false,
                searchTimeout = 5,
                providers = new[] { "pod" },
            });
        Assert.True(
            searchResponse.IsSuccessStatusCode,
            $"Search request failed: {(int)searchResponse.StatusCode} {await searchResponse.Content.ReadAsStringAsync()}");

        using var searchJson = JsonDocument.Parse(await searchResponse.Content.ReadAsStringAsync());
        var searchId = searchJson.RootElement.GetProperty("id").GetGuid();

        var itemId = await WaitForMeshSearchResultAsync(alphaClient, searchId, probeFilename, contentId);
        var downloadResponse = await alphaClient.PostAsync($"/api/v0/searches/{searchId}/items/{itemId}/download?api_key=integration-test", content: null);
        Assert.True(
            downloadResponse.IsSuccessStatusCode,
            $"Download request failed: {(int)downloadResponse.StatusCode} {await downloadResponse.Content.ReadAsStringAsync()}");

        using var downloadJson = JsonDocument.Parse(await downloadResponse.Content.ReadAsStringAsync());
        Assert.True(downloadJson.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(contentId, downloadJson.RootElement.GetProperty("content_id").GetString());
        Assert.Equal("pod", downloadJson.RootElement.GetProperty("source").GetString());

        var localPath = downloadJson.RootElement.GetProperty("path").GetString();
        Assert.False(string.IsNullOrWhiteSpace(localPath));
        Assert.True(File.Exists(localPath), $"Downloaded file not found: {localPath}");
        Assert.Equal(probeBytes, await File.ReadAllBytesAsync(localPath!));
    }

    [Fact]
    public async Task OptionalLiveAccounts_CanSearchAndDownloadHostedProbeOverOverlayMesh()
    {
        if (!TryLoadLocalMeshAccounts(out var accounts))
        {
            return;
        }

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        await using var alpha = new SlskdnFullInstanceRunner(
            loggerFactory.CreateLogger<SlskdnFullInstanceRunner>(),
            $"mesh-live-alpha-{Guid.NewGuid():N}"[..24]);
        await using var beta = new SlskdnFullInstanceRunner(
            loggerFactory.CreateLogger<SlskdnFullInstanceRunner>(),
            $"mesh-live-beta-{Guid.NewGuid():N}"[..23]);

        var probeId = Guid.NewGuid().ToString("N")[..12];
        var probeFilename = $"mesh-live-probe-{probeId}.flac";
        var probeBytes = Enumerable.Range(0, 8192).Select(i => (byte)((i * 17) % 251)).ToArray();
        var betaSharePath = Path.Combine(beta.SharesDirectory, probeFilename);
        await File.WriteAllBytesAsync(betaSharePath, probeBytes);

        await alpha.StartAsync(
            disableAuthentication: true,
            noConnect: false,
            soulseekUsername: accounts.AlphaUsername,
            soulseekPassword: accounts.AlphaPassword);
        await beta.StartAsync(
            disableAuthentication: true,
            noConnect: false,
            soulseekUsername: accounts.BetaUsername,
            soulseekPassword: accounts.BetaPassword);

        using var alphaClient = new HttpClient { BaseAddress = new Uri(alpha.ApiUrl) };
        using var betaClient = new HttpClient { BaseAddress = new Uri(beta.ApiUrl) };

        await WaitForSoulseekLoggedInAsync(alphaClient, "alpha");
        await WaitForSoulseekLoggedInAsync(betaClient, "beta");

        var scanResponse = await betaClient.PutAsync("/api/v0/shares?api_key=integration-test", content: null);
        Assert.True(
            scanResponse.IsSuccessStatusCode,
            $"Share scan request failed: {(int)scanResponse.StatusCode} {await scanResponse.Content.ReadAsStringAsync()}");

        var contentId = $"content:live-test:{probeId}";
        await WaitForAsync(
            () => TrySeedContentItemAsync(beta, probeFilename, contentId),
            TimeSpan.FromSeconds(20),
            () => "beta live-account share repository did not index the probe file");

        var connectResponse = await alphaClient.PostAsJsonAsync(
            "/api/v0/overlay/connect?api_key=integration-test",
            new ConnectOverlayPeerRequest
            {
                Address = "127.0.0.1",
                Port = beta.OverlayPort!.Value,
            });
        Assert.True(
            connectResponse.IsSuccessStatusCode,
            $"Overlay connect request failed: {(int)connectResponse.StatusCode} {await connectResponse.Content.ReadAsStringAsync()}");

        await WaitForAsync(
            async () =>
            {
                var alphaConnections = await alphaClient.GetFromJsonAsync<List<MeshPeerInfoResponse>>("/api/v0/overlay/connections");
                return alphaConnections?.Exists(peer =>
                    string.Equals(peer.Username, accounts.BetaUsername, StringComparison.OrdinalIgnoreCase)) == true;
            },
            TimeSpan.FromSeconds(20),
            () => "alpha did not keep an outbound overlay connection to beta using live accounts");

        var searchResponse = await alphaClient.PostAsJsonAsync(
            "/api/v0/searches?api_key=integration-test",
            new
            {
                searchText = Path.GetFileNameWithoutExtension(probeFilename),
                filterResponses = false,
                searchTimeout = 5,
                providers = new[] { "pod" },
            });
        Assert.True(
            searchResponse.IsSuccessStatusCode,
            $"Search request failed: {(int)searchResponse.StatusCode} {await searchResponse.Content.ReadAsStringAsync()}");

        using var searchJson = JsonDocument.Parse(await searchResponse.Content.ReadAsStringAsync());
        var searchId = searchJson.RootElement.GetProperty("id").GetGuid();

        var itemId = await WaitForMeshSearchResultAsync(alphaClient, searchId, probeFilename, contentId, accounts.BetaUsername);
        var downloadResponse = await alphaClient.PostAsync($"/api/v0/searches/{searchId}/items/{itemId}/download?api_key=integration-test", content: null);
        Assert.True(
            downloadResponse.IsSuccessStatusCode,
            $"Download request failed: {(int)downloadResponse.StatusCode} {await downloadResponse.Content.ReadAsStringAsync()}");

        using var downloadJson = JsonDocument.Parse(await downloadResponse.Content.ReadAsStringAsync());
        Assert.True(downloadJson.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(contentId, downloadJson.RootElement.GetProperty("content_id").GetString());
        Assert.Equal("pod", downloadJson.RootElement.GetProperty("source").GetString());

        var localPath = downloadJson.RootElement.GetProperty("path").GetString();
        Assert.False(string.IsNullOrWhiteSpace(localPath));
        Assert.True(File.Exists(localPath), $"Downloaded file not found: {localPath}");
        Assert.Equal(probeBytes, await File.ReadAllBytesAsync(localPath!));
    }

    private static async Task WaitForAsync(Func<Task<bool>> predicate, TimeSpan timeout, Func<string> failureMessageFactory)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await predicate())
            {
                return;
            }

            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(remaining < TimeSpan.FromMilliseconds(250) ? remaining : TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException(failureMessageFactory());
    }

    private static async Task<string> WaitForMeshSearchResultAsync(HttpClient client, Guid searchId, string expectedFilename, string expectedContentId)
    {
        return await WaitForMeshSearchResultAsync(client, searchId, expectedFilename, expectedContentId, "mesh-beta");
    }

    private static async Task<string> WaitForMeshSearchResultAsync(HttpClient client, Guid searchId, string expectedFilename, string expectedContentId, string expectedUsername)
    {
        string? failureDetails = null;

        await WaitForAsync(
            async () =>
            {
                using var response = await client.GetAsync($"/api/v0/searches/{searchId}?includeResponses=true");
                var body = await response.Content.ReadAsStringAsync();
                failureDetails = body;
                response.EnsureSuccessStatusCode();

                using var document = JsonDocument.Parse(body);
                if (!document.RootElement.TryGetProperty("responses", out var responses) ||
                    responses.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                var responseIndex = 0;
                foreach (var searchResponse in responses.EnumerateArray())
                {
                    var username = searchResponse.TryGetProperty("username", out var usernameElement)
                        ? usernameElement.GetString()
                        : null;
                    var primarySource = searchResponse.TryGetProperty("primarySource", out var primarySourceElement)
                        ? primarySourceElement.GetString()
                        : null;

                    if (!string.Equals(username, expectedUsername, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(primarySource, "pod", StringComparison.OrdinalIgnoreCase))
                    {
                        responseIndex++;
                        continue;
                    }

                    if (!searchResponse.TryGetProperty("files", out var files) ||
                        files.ValueKind != JsonValueKind.Array)
                    {
                        responseIndex++;
                        continue;
                    }

                    var fileIndex = 0;
                    foreach (var file in files.EnumerateArray())
                    {
                        var filename = file.TryGetProperty("filename", out var filenameElement)
                            ? filenameElement.GetString()
                            : null;
                        var contentId = file.TryGetProperty("contentId", out var contentIdElement)
                            ? contentIdElement.GetString()
                            : null;

                        if (filename?.EndsWith(expectedFilename, StringComparison.OrdinalIgnoreCase) == true &&
                            string.Equals(contentId, expectedContentId, StringComparison.Ordinal))
                        {
                            failureDetails = $"{responseIndex}:{fileIndex}";
                            return true;
                        }

                        fileIndex++;
                    }

                    responseIndex++;
                }

                return false;
            },
            TimeSpan.FromSeconds(20),
            () => "mesh search result did not include beta's content-routed probe file\n" + failureDetails);

        return failureDetails!;
    }

    private static async Task WaitForSoulseekLoggedInAsync(HttpClient client, string nodeName)
    {
        string? failureDetails = null;
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(90);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync("/api/v0/application");
            var body = await response.Content.ReadAsStringAsync();
            failureDetails = body;

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                continue;
            }

            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(body);
            if (document.RootElement
                .GetProperty("server")
                .GetProperty("isLoggedIn")
                .GetBoolean())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        Assert.Fail($"{nodeName} did not log in to Soulseek with the configured live test account\n" + failureDetails);
    }

    private static bool TryLoadLocalMeshAccounts(out LocalMeshAccounts accounts)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var envPath = Path.Combine(AppContext.BaseDirectory, "local-mesh-accounts.env");
        if (!File.Exists(envPath))
        {
            envPath = Path.Combine(FindRepositoryRoot(), "tests", "slskd.Tests.Integration", "local-mesh-accounts.env");
        }

        if (File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                values[trimmed[..separatorIndex].Trim()] = trimmed[(separatorIndex + 1)..].Trim();
            }
        }

        var alphaUsername = ReadCredential(values, "SLSKDN_MESH_ACCOUNT_A_USERNAME");
        var alphaPassword = ReadCredential(values, "SLSKDN_MESH_ACCOUNT_A_PASSWORD");
        var betaUsername = ReadCredential(values, "SLSKDN_MESH_ACCOUNT_B_USERNAME");
        var betaPassword = ReadCredential(values, "SLSKDN_MESH_ACCOUNT_B_PASSWORD");

        accounts = new LocalMeshAccounts(alphaUsername, alphaPassword, betaUsername, betaPassword);
        return !string.IsNullOrWhiteSpace(alphaUsername) &&
            !string.IsNullOrWhiteSpace(alphaPassword) &&
            !string.IsNullOrWhiteSpace(betaUsername) &&
            !string.IsNullOrWhiteSpace(betaPassword);
    }

    private static string ReadCredential(Dictionary<string, string> values, string key)
    {
        var envValue = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue;
        }

        return values.TryGetValue(key, out var fileValue) ? fileValue : string.Empty;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "slskd.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private sealed record LocalMeshAccounts(
        string AlphaUsername,
        string AlphaPassword,
        string BetaUsername,
        string BetaPassword);

    private static Task<bool> TrySeedContentItemAsync(SlskdnFullInstanceRunner runner, string filename, string contentId)
    {
        var databasePath = Path.Combine(runner.DataDirectory, "shares.local.db");
        if (!File.Exists(databasePath))
        {
            return Task.FromResult(false);
        }

        using var repository = new SqliteShareRepository($"Data Source={databasePath};Cache=shared");
        var file = repository.ListFiles(includeFullPath: true).FirstOrDefault(f =>
            f.Filename.EndsWith(filename, StringComparison.OrdinalIgnoreCase));
        if (file == null)
        {
            return Task.FromResult(false);
        }

        repository.UpsertContentItem(
            contentId,
            "test",
            "probe",
            file.Filename,
            isAdvertisable: true,
            moderationReason: null,
            checkedAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        return Task.FromResult(true);
    }

    private static async Task<string> BuildFailureDetailsAsync(
        HttpClient alphaClient,
        HttpClient betaClient,
        List<MeshPeerInfoResponse>? alphaConnections = null,
        List<MeshPeerInfoResponse>? betaConnections = null,
        PeerStatistics? alphaPeerStats = null,
        PeerStatistics? betaPeerStats = null)
    {
        alphaConnections ??= await alphaClient.GetFromJsonAsync<List<MeshPeerInfoResponse>>("/api/v0/overlay/connections");
        betaConnections ??= await betaClient.GetFromJsonAsync<List<MeshPeerInfoResponse>>("/api/v0/overlay/connections");
        alphaPeerStats ??= await alphaClient.GetFromJsonAsync<PeerStatistics>("/api/v0/security/peers/stats");
        betaPeerStats ??= await betaClient.GetFromJsonAsync<PeerStatistics>("/api/v0/security/peers/stats");
        var alphaOverlayStats = await alphaClient.GetFromJsonAsync<OverlayStatsResponse>("/api/v0/overlay/stats");
        var betaOverlayStats = await betaClient.GetFromJsonAsync<OverlayStatsResponse>("/api/v0/overlay/stats");
        var alphaDhtStatus = await alphaClient.GetFromJsonAsync<DhtStatusResponse>("/api/v0/dht/status");
        var betaDhtStatus = await betaClient.GetFromJsonAsync<DhtStatusResponse>("/api/v0/dht/status");

        return JsonSerializer.Serialize(new
        {
            alpha = new { connections = alphaConnections, peerStats = alphaPeerStats, overlay = alphaOverlayStats, dht = alphaDhtStatus },
            beta = new { connections = betaConnections, peerStats = betaPeerStats, overlay = betaOverlayStats, dht = betaDhtStatus },
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
