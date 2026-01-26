// <copyright file="MeshSearchLoopbackTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Integration.DhtRendezvous;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using slskd.DhtRendezvous;
using slskd.DhtRendezvous.Messages;
using slskd.DhtRendezvous.Search;
using slskd.DhtRendezvous.Security;
using slskd.Shares;
using Soulseek;
using Xunit;

/// <summary>
/// Loopback integration test: overlay server + client, mesh_search_req -> mesh_search_resp.
/// Runs under timeout and ensures connection closes cleanly.
/// </summary>
[Trait("Category", "L2-Integration")]
[Trait("Category", "DhtRendezvous")]
public class MeshSearchLoopbackTests
{
    private const int TestOverlayPort = 50499;
    private const int TimeoutSeconds = 10;

    [Fact]
    public async Task MeshSearchReq_OverLoopback_ReturnsMeshSearchResp()
    {
            var tempDir = Path.Combine(Path.GetTempPath(), "slskdn-mesh-lb-" + Guid.NewGuid().ToString("N")[..8]);
            System.IO.Directory.CreateDirectory(tempDir);
        try
        {
            // Find an available port (50499 may be in use)
            var port = GetAvailablePort(TestOverlayPort, 50);
            var dhtOpts = new DhtRendezvousOptions { OverlayPort = port };

            var opts = new slskd.Options { Soulseek = new slskd.Options.SoulseekOptions { Username = "loopback-server" } };
            var optsMonitor = new Mock<IOptionsMonitor<slskd.Options>>();
            optsMonitor.Setup(m => m.CurrentValue).Returns(opts);

            var logFact = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning));
            var certMgr = new CertificateManager(logFact.CreateLogger<CertificateManager>(), tempDir);
            var pinStore = new CertificatePinStore(NullLoggerFactory.Instance.CreateLogger<CertificatePinStore>(), tempDir);
            using var rateLimiter = new OverlayRateLimiter();
            using var blocklist = new OverlayBlocklist(logFact.CreateLogger<OverlayBlocklist>());
            var registry = new MeshNeighborRegistry(logFact.CreateLogger<MeshNeighborRegistry>());

            var meshSync = new Mock<slskd.Mesh.IMeshSyncService>();
            meshSync.Setup(m => m.HandleMessageAsync(It.IsAny<string>(), It.IsAny<slskd.Mesh.Messages.MeshMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((slskd.Mesh.Messages.MeshMessage)null!);

            var share = new Mock<IShareService>();
            share.Setup(s => s.SearchLocalAsync(It.IsAny<Soulseek.SearchQuery>()))
                .ReturnsAsync(new List<Soulseek.File> { new Soulseek.File(1, "loop\\test.flac", 1000, ".flac") });

            var handler = new MeshSearchRpcHandler(share.Object, logFact.CreateLogger<MeshSearchRpcHandler>());

            var server = new MeshOverlayServer(
                logFact.CreateLogger<MeshOverlayServer>(),
                optsMonitor.Object,
                certMgr,
                pinStore,
                rateLimiter,
                blocklist,
                registry,
                meshSync.Object,
                handler,
                dhtOpts);

            await server.StartAsync(CancellationToken.None);

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
                var serverCert = certMgr.GetOrCreateServerCertificate();
                var client = await MeshOverlayConnection.ConnectAsync(
                    new IPEndPoint(IPAddress.Loopback, port),
                    serverCert,
                    cts.Token);
                await using (client.ConfigureAwait(false))
                {
                    await client.PerformClientHandshakeAsync("loopback-client", cancellationToken: cts.Token);

                    var req = new MeshSearchRequestMessage
                    {
                        RequestId = Guid.NewGuid().ToString("N"),
                        SearchText = "test",
                        MaxResults = 10,
                    };
                    await client.WriteMessageAsync(req, cts.Token);
                    var resp = await client.ReadMessageAsync<MeshSearchResponseMessage>(cts.Token);

                    Assert.NotNull(resp);
                    Assert.Equal(req.RequestId, resp.RequestId);
                    Assert.Null(resp.Error);
                    Assert.Single(resp.Files);
                    Assert.Equal("loop\\test.flac", resp.Files![0].Filename);
                    Assert.Equal(1000, resp.Files[0].Size);
                }
            }
            finally
            {
                await server.StopAsync();
            }
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
            {
                try { System.IO.Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
            }
        }
    }

    private static int GetAvailablePort(int start, int range)
    {
        for (int p = start; p < start + range; p++)
        {
            try
            {
                using var l = new TcpListener(IPAddress.Loopback, p);
                l.Start();
                int port = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
                return port;
            }
            catch
            {
                /* try next */
            }
        }
        return start;
    }
}
