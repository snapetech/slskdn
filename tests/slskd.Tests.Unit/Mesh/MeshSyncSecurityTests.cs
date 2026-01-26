// <copyright file="MeshSyncSecurityTests.cs" company="slskdn Team">
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

namespace slskd.Tests.Unit.Mesh
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
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
    ///     Unit tests for mesh sync security features (T-1437).
    ///     Tests signature verification, reputation checks, rate limiting, and quarantine.
    /// </summary>
    public class MeshSyncSecurityTests
    {
        private readonly Mock<IHashDbService> mockHashDb;
        private readonly Mock<ICapabilityService> mockCapabilities;
        private readonly Mock<ISoulseekClient> mockSoulseekClient;
        private readonly Mock<IMeshMessageSigner> mockMessageSigner;
        private readonly PeerReputation peerReputation;
        private readonly Mock<ILogger<MeshSyncService>> mockLogger;
        private readonly MeshSyncService meshSyncService;

        /// <summary>Used by TestableMeshSyncService to control QueryPeerForHashAsync results for consensus tests.</summary>
        private static readonly ConcurrentDictionary<string, MeshHashEntry> ConsensusQueryResponses = new();

        public MeshSyncSecurityTests()
        {
            mockHashDb = new Mock<IHashDbService>();
            mockCapabilities = new Mock<ICapabilityService>();
            mockSoulseekClient = new Mock<ISoulseekClient>();
            mockMessageSigner = new Mock<IMeshMessageSigner>();
            peerReputation = new PeerReputation(Mock.Of<ILogger<PeerReputation>>());
            mockLogger = new Mock<ILogger<MeshSyncService>>();

            // Setup default mocks
            mockHashDb.Setup(h => h.CurrentSeqId).Returns(100);
            mockHashDb.Setup(h => h.GetEntriesSinceSeqAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<HashDbEntry>());
            mockHashDb.Setup(h => h.MergeEntriesFromMeshAsync(It.IsAny<IEnumerable<HashDbEntry>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<HashDbEntry> entries, CancellationToken ct) => entries.Count());
            
            // Setup UpdatePeerLastSeqSeenAsync which is called in MergeEntriesAsync
            mockHashDb.Setup(h => h.UpdatePeerLastSeqSeenAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            // Setup capabilities mock
            mockCapabilities.Setup(c => c.VersionString).Returns("1.0.0-test");
            
            // Setup hashDb GetStats() for GenerateHelloMessage
            var dbStats = new slskd.HashDb.HashDbStats { TotalHashEntries = 1000 };
            mockHashDb.Setup(h => h.GetStats()).Returns(dbStats);

            meshSyncService = new MeshSyncService(
                mockHashDb.Object,
                mockCapabilities.Object,
                mockSoulseekClient.Object,
                mockMessageSigner.Object,
                peerReputation);
        }

        #region Signature Verification Tests (T-1430)

        [Fact]
        public async Task HandleMessageAsync_RejectsMessageWithInvalidSignature()
        {
            // Arrange
            var message = new MeshHelloMessage
            {
                ClientId = "test-peer",
                ClientVersion = "1.0.0",
                LatestSeqId = 50,
                HashCount = 1000,
            };

            mockMessageSigner.Setup(s => s.VerifyMessage(It.IsAny<MeshMessage>())).Returns(false);

            // Act
            var result = await meshSyncService.HandleMessageAsync("test-peer", message);

            // Assert
            Assert.Null(result);
            Assert.True(meshSyncService.Stats.SignatureVerificationFailures > 0);
            Assert.True(meshSyncService.Stats.RejectedMessages > 0);
        }

        [Fact]
        public async Task HandleMessageAsync_AcceptsMessageWithValidSignature()
        {
            // Arrange
            var message = new MeshHelloMessage
            {
                ClientId = "test-peer",
                ClientVersion = "1.0.0",
                LatestSeqId = 50,
                HashCount = 1000,
            };

            mockMessageSigner.Setup(s => s.VerifyMessage(It.IsAny<MeshMessage>())).Returns(true);
            mockMessageSigner.Setup(s => s.SignMessage(It.IsAny<MeshMessage>())).Returns(message);

            // Act
            var result = await meshSyncService.HandleMessageAsync("test-peer", message);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, meshSyncService.Stats.SignatureVerificationFailures);
        }

        [Fact]
        public async Task HandleMessageAsync_RejectsUnsignedMessage()
        {
            // Arrange
            var message = new MeshHelloMessage
            {
                ClientId = "test-peer",
                ClientVersion = "1.0.0",
                LatestSeqId = 50,
                HashCount = 1000,
                PublicKey = null, // No signature
                Signature = null,
            };

            mockMessageSigner.Setup(s => s.VerifyMessage(It.IsAny<MeshMessage>())).Returns(false);

            // Act
            var result = await meshSyncService.HandleMessageAsync("test-peer", message);

            // Assert
            Assert.Null(result);
            Assert.True(meshSyncService.Stats.SignatureVerificationFailures > 0);
        }

        #endregion

        #region Reputation Checks Tests (T-1431)

        [Fact]
        public async Task MergeEntriesAsync_RejectsEntriesFromUntrustedPeer()
        {
            // Arrange
            var entries = new List<MeshHashEntry>
            {
                new MeshHashEntry
                {
                    FlacKey = "0123456789abcdef", // 16 hex chars (64-bit)
                    ByteHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", // 64 hex chars (SHA256)
                    Size = 1024,
                    SeqId = 1,
                },
            };

            // Set untrusted peer reputation
            peerReputation.SetScore("untrusted-peer", 15, "Test setup");

            // Act
            var result = await meshSyncService.MergeEntriesAsync("untrusted-peer", entries);

            // Assert
            Assert.Equal(0, result);
            Assert.True(meshSyncService.Stats.ReputationBasedRejections > 0);
        }

        [Fact]
        public async Task MergeEntriesAsync_AcceptsEntriesFromTrustedPeer()
        {
            // Arrange
            var entries = new List<MeshHashEntry>
            {
                new MeshHashEntry
                {
                    FlacKey = "0123456789abcdef", // 16 hex chars (64-bit)
                    ByteHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", // 64 hex chars (SHA256 = 32 bytes)
                    Size = 1024,
                    SeqId = 1,
                },
            };

            // Set trusted peer reputation
            peerReputation.SetScore("trusted-peer", 80, "Test setup");
            
            // Reset and setup the mock to ensure it's called correctly
            mockHashDb.Reset();
            mockHashDb.Setup(h => h.CurrentSeqId).Returns(100);
            mockHashDb.Setup(h => h.GetStats()).Returns(new slskd.HashDb.HashDbStats { TotalHashEntries = 1000 });
            mockHashDb.Setup(h => h.GetEntriesSinceSeqAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<HashDbEntry>());
            mockHashDb.Setup(h => h.MergeEntriesFromMeshAsync(It.IsAny<IEnumerable<HashDbEntry>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<HashDbEntry> entries, CancellationToken ct) => 
                {
                    var entryList = entries.ToList();
                    return entryList.Count;
                });
            // Ensure UpdatePeerLastSeqSeenAsync is mocked (it's called conditionally)
            mockHashDb.Setup(h => h.UpdatePeerLastSeqSeenAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await meshSyncService.MergeEntriesAsync("trusted-peer", entries);

            // Assert
            Assert.Equal(1, result);
            Assert.Equal(0, meshSyncService.Stats.ReputationBasedRejections);
        }

        [Fact]
        public async Task MergeEntriesAsync_HandlesNullReputationService()
        {
            // Arrange - Create a new service instance without reputation
            var mockHashDbForNullTest = new Mock<IHashDbService>();
            mockHashDbForNullTest.Setup(h => h.CurrentSeqId).Returns(100);
            mockHashDbForNullTest.Setup(h => h.GetStats()).Returns(new slskd.HashDb.HashDbStats { TotalHashEntries = 1000 });
            mockHashDbForNullTest.Setup(h => h.GetEntriesSinceSeqAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<HashDbEntry>());
            mockHashDbForNullTest.Setup(h => h.MergeEntriesFromMeshAsync(It.IsAny<IEnumerable<HashDbEntry>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<HashDbEntry> entries, CancellationToken ct) => 
                {
                    var entryList = entries.ToList();
                    // Return the count of entries that were actually passed to merge
                    return entryList.Count;
                });
            
            // Also need to setup UpdatePeerLastSeqSeenAsync which is called in MergeEntriesAsync
            mockHashDbForNullTest.Setup(h => h.UpdatePeerLastSeqSeenAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            var serviceWithoutReputation = new MeshSyncService(
                mockHashDbForNullTest.Object,
                mockCapabilities.Object,
                mockSoulseekClient.Object,
                mockMessageSigner.Object,
                peerReputation: null);

            var entries = new List<MeshHashEntry>
            {
                new MeshHashEntry
                {
                    FlacKey = "0123456789abcdef", // 16 hex chars (64-bit)
                    ByteHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", // 64 hex chars (SHA256 = 32 bytes)
                    Size = 1024,
                    SeqId = 1,
                },
            };

            // Act
            var result = await serviceWithoutReputation.MergeEntriesAsync("test-peer", entries);

            // Assert
            Assert.Equal(1, result); // Should work without reputation service
        }

        #endregion

        #region Rate Limiting Tests (T-1432)

        [Fact]
        public async Task MergeEntriesAsync_RejectsWhenRateLimitExceeded()
        {
            // Arrange
            var entries = Enumerable.Range(0, 100).Select(i => new MeshHashEntry
            {
                FlacKey = "invalid", // Invalid FLAC key to trigger validation failures
                ByteHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                Size = 1024,
                SeqId = i,
            }).ToList();

            // Peer is not untrusted (default reputation is 50)

            // Act - Send enough invalid entries to exceed rate limit (50 per 5-minute window)
            var result = await meshSyncService.MergeEntriesAsync("rate-limited-peer", entries);

            // Assert
            // Should reject after rate limit is exceeded
            Assert.True(meshSyncService.Stats.RateLimitViolations > 0);
        }

        [Fact]
        public async Task HandleMessageAsync_RejectsWhenMessageRateLimitExceeded()
        {
            // Arrange
            mockMessageSigner.Setup(s => s.VerifyMessage(It.IsAny<MeshMessage>())).Returns(true);

            // Create invalid messages (will fail validation)
            for (int i = 0; i < 15; i++) // Exceed MaxInvalidMessagesPerWindow (10)
            {
                var invalidMessage = new MeshReqDeltaMessage
                {
                    SinceSeqId = -1, // Invalid: negative sequence ID
                    MaxEntries = 1000,
                };

                await meshSyncService.HandleMessageAsync("rate-limited-peer", invalidMessage);
            }

            // Assert
            Assert.True(meshSyncService.Stats.RateLimitViolations > 0);
        }

        #endregion

        #region Quarantine Tests (T-1433)

        [Fact]
        public async Task MergeEntriesAsync_RejectsQuarantinedPeer()
        {
            // Use QuarantineViolationThreshold=1 so one rate-limit violation triggers quarantine (deterministic).
            // With default 3, we would need 3 separate batches; the sliding-window logic can be timing-sensitive.
            var opts = Options.Create(new MeshSyncSecurityOptions { QuarantineViolationThreshold = 1 });
            var svc = CreateMeshSyncService(syncSecurityOptions: opts);

            var validEntries = new List<MeshHashEntry>
            {
                new MeshHashEntry
                {
                    FlacKey = "0123456789abcdef",
                    ByteHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                    Size = 1024,
                    SeqId = 1,
                },
            };

            // One batch of 60 invalid entries: all fail FlacKey, RecordInvalidEntries(60), IsRateLimited (>=50),
            // RecordRateLimitViolation, ShouldQuarantine (1>=1) → QuarantinePeer, QuarantineEvents++
            var invalidEntries = Enumerable.Range(0, 60).Select(j => new MeshHashEntry
            {
                FlacKey = "invalid",
                ByteHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                Size = 1024,
                SeqId = j,
            }).ToList();

            var mergeResult = await svc.MergeEntriesAsync("quarantined-peer", invalidEntries);
            Assert.Equal(0, mergeResult);
            Assert.True(svc.Stats.QuarantineEvents >= 1,
                "Quarantine should have been triggered by one rate-limit violation (QuarantineViolationThreshold=1).");

            // Act - merge from same peer; should be rejected (IsQuarantined at start of MergeEntriesAsync)
            var result = await svc.MergeEntriesAsync("quarantined-peer", validEntries);

            Assert.Equal(0, result);
        }

        [Fact]
        public async Task HandleMessageAsync_RejectsQuarantinedPeer()
        {
            // Arrange
            mockMessageSigner.Setup(s => s.VerifyMessage(It.IsAny<MeshMessage>())).Returns(true);

            // Trigger multiple rate limit violations to cause quarantine
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 15; j++)
                {
                    var invalidMessage = new MeshReqDeltaMessage
                    {
                        SinceSeqId = -1,
                        MaxEntries = 1000,
                    };

                    await meshSyncService.HandleMessageAsync("quarantined-peer", invalidMessage);
                }
            }

            // Act - Try to send message from quarantined peer
            var validMessage = new MeshHelloMessage
            {
                ClientId = "quarantined-peer",
                ClientVersion = "1.0.0",
                LatestSeqId = 50,
                HashCount = 1000,
            };

            mockMessageSigner.Setup(s => s.VerifyMessage(validMessage)).Returns(true);
            var result = await meshSyncService.HandleMessageAsync("quarantined-peer", validMessage);

            // Assert
            Assert.Null(result);
            Assert.True(meshSyncService.Stats.QuarantineEvents > 0);
        }

        #endregion

        #region Security Metrics Tests (T-1436)

        [Fact]
        public void Stats_IncludesSecurityMetrics()
        {
            // Arrange & Act
            var stats = meshSyncService.Stats;

            // Assert
            Assert.True(stats.SignatureVerificationFailures >= 0);
            Assert.True(stats.ReputationBasedRejections >= 0);
            Assert.True(stats.RateLimitViolations >= 0);
            Assert.True(stats.QuarantinedPeers >= 0);
            Assert.True(stats.QuarantineEvents >= 0);
            Assert.True(stats.ProofOfPossessionFailures >= 0);
        }

        [Fact]
        public async Task Stats_TracksQuarantinedPeersCount()
        {
            // Arrange
            // Peer is not untrusted (default reputation is 50)
            mockMessageSigner.Setup(s => s.VerifyMessage(It.IsAny<MeshMessage>())).Returns(true);

            // Trigger quarantine for a peer
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 15; j++)
                {
                    var invalidMessage = new MeshReqDeltaMessage
                    {
                        SinceSeqId = -1,
                        MaxEntries = 1000,
                    };

                    await meshSyncService.HandleMessageAsync("quarantined-peer-1", invalidMessage);
                }
            }

            // Act
            var stats = meshSyncService.Stats;

            // Assert
            Assert.True(stats.QuarantinedPeers > 0);
        }

        #endregion

        #region Proof-of-Possession Tests (T-1434)

        [Fact]
        public async Task MergeEntriesAsync_PoPEnabled_SkipsEntryWhenVerifyReturnsFalse()
        {
            var popMock = new Mock<IProofOfPossessionService>();
            popMock.Setup(p => p.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<IChunkRequestSender>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var opts = Options.Create(new MeshSyncSecurityOptions { ProofOfPossessionEnabled = true });
            var svc = CreateMeshSyncService(syncSecurityOptions: opts, proofOfPossession: popMock.Object);

            var entries = new List<MeshHashEntry>
            {
                new MeshHashEntry { FlacKey = "0123456789abcdef", ByteHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", Size = 1024, SeqId = 1 },
            };

            var merged = await svc.MergeEntriesAsync("peer", entries);

            Assert.Equal(0, merged);
            Assert.True(svc.Stats.ProofOfPossessionFailures >= 1);
        }

        [Fact]
        public async Task MergeEntriesAsync_PoPEnabled_MergesWhenVerifyReturnsTrue()
        {
            var popMock = new Mock<IProofOfPossessionService>();
            popMock.Setup(p => p.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<IChunkRequestSender>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var opts = Options.Create(new MeshSyncSecurityOptions { ProofOfPossessionEnabled = true });
            var svc = CreateMeshSyncService(syncSecurityOptions: opts, proofOfPossession: popMock.Object);

            var entries = new List<MeshHashEntry>
            {
                new MeshHashEntry { FlacKey = "0123456789abcdef", ByteHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", Size = 1024, SeqId = 1 },
            };

            var merged = await svc.MergeEntriesAsync("peer", entries);

            Assert.Equal(1, merged);
            Assert.Equal(0, svc.Stats.ProofOfPossessionFailures);
        }

        [Fact]
        public async Task MergeEntriesAsync_PoPDisabled_DoesNotCallProofOfPossession()
        {
            var popMock = new Mock<IProofOfPossessionService>();
            var opts = Options.Create(new MeshSyncSecurityOptions { ProofOfPossessionEnabled = false });
            var svc = CreateMeshSyncService(syncSecurityOptions: opts, proofOfPossession: popMock.Object);

            var entries = new List<MeshHashEntry>
            {
                new MeshHashEntry { FlacKey = "0123456789abcdef", ByteHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", Size = 1024, SeqId = 1 },
            };

            var merged = await svc.MergeEntriesAsync("peer", entries);

            Assert.Equal(1, merged);
            popMock.Verify(p => p.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<IChunkRequestSender>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Consensus Tests (T-1435)

        [Fact]
        public async Task LookupHashAsync_ReturnsNull_WhenNoMeshPeersAndNotInLocalDb()
        {
            mockHashDb.Setup(h => h.LookupHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((HashDbEntry)null);

            var result = await meshSyncService.LookupHashAsync("0123456789abcdef");

            Assert.Null(result);
        }

        [Fact]
        public async Task LookupHashAsync_ReturnsLocal_WhenFoundInDb()
        {
            var local = new HashDbEntry { FlacKey = "0123456789abcdef", ByteHash = "ab", Size = 1024, SeqId = 1 };
            mockHashDb.Setup(h => h.LookupHashAsync("0123456789abcdef", It.IsAny<CancellationToken>())).ReturnsAsync(local);

            var result = await meshSyncService.LookupHashAsync("0123456789abcdef");

            Assert.NotNull(result);
            Assert.Equal("0123456789abcdef", result.FlacKey);
            Assert.Equal(1024, result.Size);
        }

        [Fact]
        public async Task LookupHashAsync_ConsensusOptions_WhenMinAgreementsMet_ReturnsEntry()
        {
            var flacKey = "0123456789abcdef";
            var agreed = new MeshHashEntry { FlacKey = flacKey, ByteHash = "ab".PadRight(64, '0'), Size = 100, SeqId = 1 };
            ConsensusQueryResponses["p1"] = agreed;
            ConsensusQueryResponses["p2"] = agreed;
            ConsensusQueryResponses["p3"] = null; // p3 returns null
            try
            {
                var opts = Options.Create(new MeshSyncSecurityOptions { ConsensusMinPeers = 3, ConsensusMinAgreements = 2 });
                var svc = CreateTestableMeshSyncService(opts);
                SeedPeers(svc, "p1", "p2", "p3");

                var result = await svc.LookupHashAsync(flacKey);

                Assert.NotNull(result);
                Assert.Equal(flacKey, result.FlacKey);
                Assert.Equal(100, result.Size);
            }
            finally
            {
                ConsensusQueryResponses.Clear();
            }
        }

        [Fact]
        public async Task LookupHashAsync_ConsensusOptions_WhenMinAgreementsNotMet_ReturnsNull()
        {
            var flacKey = "0123456789abcdef";
            var entry = new MeshHashEntry { FlacKey = flacKey, ByteHash = "ab".PadRight(64, '0'), Size = 100, SeqId = 1 };
            ConsensusQueryResponses["p1"] = entry;
            ConsensusQueryResponses["p2"] = entry;
            // p3 not set -> null; need 3 agreements, only 2 agree
            try
            {
                var opts = Options.Create(new MeshSyncSecurityOptions { ConsensusMinPeers = 3, ConsensusMinAgreements = 3 });
                var svc = CreateTestableMeshSyncService(opts);
                SeedPeers(svc, "p1", "p2", "p3");

                var result = await svc.LookupHashAsync(flacKey);

                Assert.Null(result);
            }
            finally
            {
                ConsensusQueryResponses.Clear();
            }
        }

        #endregion

        private MeshSyncService CreateMeshSyncService(
            IOptions<MeshSyncSecurityOptions> syncSecurityOptions = null,
            IProofOfPossessionService proofOfPossession = null)
        {
            var h = new Mock<IHashDbService>();
            h.Setup(x => x.CurrentSeqId).Returns(100);
            h.Setup(x => x.GetStats()).Returns(new slskd.HashDb.HashDbStats { TotalHashEntries = 1000 });
            h.Setup(x => x.GetEntriesSinceSeqAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<HashDbEntry>());
            h.Setup(x => x.MergeEntriesFromMeshAsync(It.IsAny<IEnumerable<HashDbEntry>>(), It.IsAny<CancellationToken>())).ReturnsAsync((IEnumerable<HashDbEntry> e, CancellationToken _) => e.Count());
            h.Setup(x => x.UpdatePeerLastSeqSeenAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            return new MeshSyncService(
                h.Object,
                mockCapabilities.Object,
                mockSoulseekClient.Object,
                mockMessageSigner.Object,
                peerReputation,
                appState: null,
                syncSecurityOptions,
                pathResolver: null,
                proofOfPossession);
        }

        private static TestableMeshSyncService CreateTestableMeshSyncService(IOptions<MeshSyncSecurityOptions> options)
        {
            var h = new Mock<IHashDbService>();
            h.Setup(x => x.CurrentSeqId).Returns(100);
            h.Setup(x => x.GetStats()).Returns(new slskd.HashDb.HashDbStats { TotalHashEntries = 1000 });
            h.Setup(x => x.GetEntriesSinceSeqAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<HashDbEntry>());
            h.Setup(x => x.MergeEntriesFromMeshAsync(It.IsAny<IEnumerable<HashDbEntry>>(), It.IsAny<CancellationToken>())).ReturnsAsync((IEnumerable<HashDbEntry> e, CancellationToken _) => e.Count());
            h.Setup(x => x.UpdatePeerLastSeqSeenAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            h.Setup(x => x.LookupHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((HashDbEntry)null);
            h.Setup(x => x.StoreHashAsync(It.IsAny<HashDbEntry>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var cap = new Mock<ICapabilityService>();
            cap.Setup(c => c.VersionString).Returns("1.0.0-test");
            cap.Setup(c => c.GetPeerCapabilities(It.IsAny<string>())).Returns(new PeerCapabilities { Flags = PeerCapabilityFlags.SupportsMeshSync });

            var signer = new Mock<IMeshMessageSigner>();
            signer.Setup(s => s.VerifyMessage(It.IsAny<MeshMessage>())).Returns(true);

            var rep = new PeerReputation(Mock.Of<ILogger<PeerReputation>>());
            return new TestableMeshSyncService(
                h.Object,
                cap.Object,
                Mock.Of<ISoulseekClient>(),
                signer.Object,
                rep,
                appState: null,
                options,
                pathResolver: null,
                proofOfPossession: null);
        }

        private static void SeedPeers(MeshSyncService svc, params string[] usernames)
        {
            foreach (var u in usernames)
            {
                var hello = new MeshHelloMessage { ClientId = u, ClientVersion = "1.0", LatestSeqId = 0, HashCount = 0 };
                _ = svc.HandleMessageAsync(u, hello).GetAwaiter().GetResult();
            }
        }

        private sealed class TestableMeshSyncService : MeshSyncService
        {
            public TestableMeshSyncService(
                IHashDbService hashDb,
                ICapabilityService capabilities,
                ISoulseekClient soulseekClient,
                IMeshMessageSigner messageSigner,
                slskd.Common.Security.PeerReputation peerReputation,
                IManagedState<State> appState,
                IOptions<MeshSyncSecurityOptions> syncSecurityOptions,
                IFlacKeyToPathResolver pathResolver,
                IProofOfPossessionService proofOfPossession)
                : base(hashDb, capabilities, soulseekClient, messageSigner, peerReputation, appState, syncSecurityOptions, pathResolver, proofOfPossession)
            { }

            protected override async Task<MeshHashEntry> QueryPeerForHashAsync(string username, string flacKey, CancellationToken cancellationToken)
            {
                await Task.CompletedTask;
                return ConsensusQueryResponses.TryGetValue(username, out var e) ? e : null;
            }
        }
    }
}

