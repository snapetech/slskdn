// <copyright file="MeshSyncSecurityIntegrationTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.Tests.Integration.Mesh;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Capabilities;
using slskd.Common.Security;
using slskd.Core;
using slskd.HashDb;
using slskd.HashDb.Models;
using slskd.Mesh;
using slskd.Mesh.Messages;
using Soulseek;
using Xunit;

/// <summary>
///     Integration tests for mesh sync security (T-1438): signature verification, reputation,
///     rate limiting, quarantine, and security metrics when processing entries and messages.
/// </summary>
[Trait("Category", "L2-Integration")]
[Trait("Category", "Mesh")]
public class MeshSyncSecurityIntegrationTests
{
    private static (MeshSyncService Service, PeerReputation Reputation) CreateService(Common.Security.PeerReputation? reputation = null)
    {
        var mockHashDb = new Mock<IHashDbService>();
        mockHashDb.Setup(h => h.CurrentSeqId).Returns(100);
        mockHashDb.Setup(h => h.GetEntriesSinceSeqAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HashDbEntry>());
        mockHashDb.Setup(h => h.MergeEntriesFromMeshAsync(It.IsAny<IEnumerable<HashDbEntry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<HashDbEntry> e, CancellationToken _) => e.Count());
        mockHashDb.Setup(h => h.UpdatePeerLastSeqSeenAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockHashDb.Setup(h => h.GetStats()).Returns(new HashDbStats { TotalHashEntries = 1000 });

        var mockCapabilities = new Mock<ICapabilityService>();
        mockCapabilities.Setup(c => c.VersionString).Returns("1.0.0-test");

        var mockSoulseek = new Mock<ISoulseekClient>();
        var mockSigner = new Mock<IMeshMessageSigner>();

        var rep = reputation ?? new PeerReputation(Mock.Of<ILogger<PeerReputation>>());
        var svc = new MeshSyncService(
            mockHashDb.Object,
            mockCapabilities.Object,
            mockSoulseek.Object,
            mockSigner.Object,
            rep);

        return (svc, rep);
    }

    /// <summary>Malicious peer floods invalid entries; rate limit and quarantine should trigger.</summary>
    [Fact]
    public async Task MergeEntriesAsync_FloodOfInvalidEntries_TriggersRateLimitAndQuarantine()
    {
        var (svc, _) = CreateService();
        var invalid = Enumerable.Range(0, 60).Select(i => new MeshHashEntry
        {
            FlacKey = "invalid",
            ByteHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            Size = 1024,
            SeqId = i,
        }).ToList();

        for (int i = 0; i < 5; i++)
        {
            var r = await svc.MergeEntriesAsync("flood-peer", invalid);
            Assert.Equal(0, r);
            await Task.Delay(50);
        }

        Assert.True(svc.Stats.RateLimitViolations > 0, $"RateLimitViolations should be > 0, got {svc.Stats.RateLimitViolations}");
        Assert.True(svc.Stats.SkippedEntries > 0, $"SkippedEntries should be > 0, got {svc.Stats.SkippedEntries}");
    }

    /// <summary>Untrusted peer attempts to sync; rejected and reputation metric incremented.</summary>
    [Fact]
    public async Task MergeEntriesAsync_UntrustedPeer_RejectedAndReputationMetricIncremented()
    {
        var (svc, rep) = CreateService();
        rep.SetScore("untrusted-peer", 15, "test");

        var entries = new List<MeshHashEntry>
        {
            new()
            {
                FlacKey = "0123456789abcdef",
                ByteHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                Size = 1024,
                SeqId = 1,
            },
        };

        var r = await svc.MergeEntriesAsync("untrusted-peer", entries);
        Assert.Equal(0, r);
        Assert.True(svc.Stats.ReputationBasedRejections > 0);
    }

    /// <summary>Invalid signature on message leads to rejection and signature metric.</summary>
    [Fact]
    public async Task HandleMessageAsync_InvalidSignature_RejectedAndSignatureMetricIncremented()
    {
        var (svc, _) = CreateService();
        var mockSigner = new Mock<IMeshMessageSigner>();
        // Re-create service with signer that always fails (we cannot replace on existing svc)
        var mockHashDb = new Mock<IHashDbService>();
        mockHashDb.Setup(h => h.CurrentSeqId).Returns(100);
        mockHashDb.Setup(h => h.GetStats()).Returns(new HashDbStats { TotalHashEntries = 1000 });
        var mockCap = new Mock<ICapabilityService>();
        mockCap.Setup(c => c.VersionString).Returns("1.0.0");
        mockSigner.Setup(s => s.VerifyMessage(It.IsAny<MeshMessage>())).Returns(false);
        var svc2 = new MeshSyncService(
            mockHashDb.Object,
            mockCap.Object,
            Mock.Of<ISoulseekClient>(),
            mockSigner.Object,
            new PeerReputation(Mock.Of<ILogger<PeerReputation>>()));

        var msg = new MeshHelloMessage { ClientId = "x", ClientVersion = "1.0", LatestSeqId = 1, HashCount = 0 };
        var res = await svc2.HandleMessageAsync("x", msg);
        Assert.Null(res);
        Assert.True(svc2.Stats.SignatureVerificationFailures > 0);
        Assert.True(svc2.Stats.RejectedMessages > 0);
    }

    /// <summary>After quarantine (via invalid messages), valid entries from same peer are rejected.</summary>
    [Fact]
    public async Task MergeEntriesAsync_AfterQuarantine_ValidEntriesFromSamePeer_Rejected()
    {
        var (svc, _) = CreateService();
        // Trigger quarantine via HandleMessageAsync (invalid MeshReqDeltaMessage): 3 rounds of 15 each
        var mockSigner = new Mock<IMeshMessageSigner>();
        mockSigner.Setup(s => s.VerifyMessage(It.IsAny<MeshMessage>())).Returns(true);
        // CreateService's signer is used by svc; we cannot replace it. Build a second service with
        // a signer that accepts, so invalid *messages* (not signature) trigger RecordInvalidMessage.
        var mockHashDb = new Mock<IHashDbService>();
        mockHashDb.Setup(h => h.CurrentSeqId).Returns(100);
        mockHashDb.Setup(h => h.GetStats()).Returns(new HashDbStats { TotalHashEntries = 1000 });
        var mockCap = new Mock<ICapabilityService>();
        mockCap.Setup(c => c.VersionString).Returns("1.0.0");
        var svc2 = new MeshSyncService(
            mockHashDb.Object,
            mockCap.Object,
            Mock.Of<ISoulseekClient>(),
            mockSigner.Object,
            new PeerReputation(Mock.Of<ILogger<PeerReputation>>()));
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 15; j++)
                await svc2.HandleMessageAsync("q-peer", new MeshReqDeltaMessage { SinceSeqId = -1, MaxEntries = 1000 });

        var valid = new List<MeshHashEntry>
        {
            new()
            {
                FlacKey = "0123456789abcdef",
                ByteHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                Size = 1024,
                SeqId = 999,
            },
        };
        // MergeEntriesFromMeshAsync and UpdatePeerLastSeqSeenAsync for the valid-merge path
        mockHashDb.Setup(h => h.MergeEntriesFromMeshAsync(It.IsAny<IEnumerable<HashDbEntry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<HashDbEntry> e, CancellationToken _) => e.Count());
        mockHashDb.Setup(h => h.UpdatePeerLastSeqSeenAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // After 3*15 invalid messages, peer should be quarantined; merge from same peer must be rejected (0).
        var r = await svc2.MergeEntriesAsync("q-peer", valid);
        Assert.Equal(0, r);
    }

    /// <summary>Stats include all security metrics.</summary>
    [Fact]
    public void Stats_IncludeSecurityMetrics()
    {
        var (svc, _) = CreateService();
        var s = svc.Stats;
        Assert.True(s.SignatureVerificationFailures >= 0);
        Assert.True(s.ReputationBasedRejections >= 0);
        Assert.True(s.RateLimitViolations >= 0);
        Assert.True(s.QuarantinedPeers >= 0);
        Assert.True(s.QuarantineEvents >= 0);
        Assert.True(s.ProofOfPossessionFailures >= 0);
        Assert.True(s.RejectedMessages >= 0);
        Assert.True(s.SkippedEntries >= 0);
    }
}
