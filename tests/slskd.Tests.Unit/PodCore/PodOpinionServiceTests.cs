// <copyright file="PodOpinionServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Mesh.Dht;
using slskd.Mesh.Transport;
using slskd.PodCore;
using Xunit;

public class PodOpinionServiceTests
{
    [Fact]
    public async Task PublishOpinionAsync_WithValidSignature_StoresTypedOpinionList()
    {
        using var ed25519 = new Ed25519Signer();
        var keyPair = ed25519.GenerateKeyPair();
        var privateKey = keyPair.PrivateKey;
        var publicKey = keyPair.PublicKey;
        var publicKeyBase64 = Convert.ToBase64String(publicKey);
        var opinion = CreateSignedOpinion(ed25519, privateKey, "pod-1", "peer-1");

        var podService = CreatePodServiceMock(publicKeyBase64);
        var dhtClient = new Mock<IMeshDhtClient>();
        object? storedValue = null;
        var expectedKey = "pod:pod-1:opinions:content:audio:track:track-1";

        dhtClient.Setup(x => x.GetAsync<List<PodVariantOpinion>>(expectedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodVariantOpinion>());
        dhtClient.Setup(x => x.PutAsync(expectedKey, It.IsAny<object?>(), 3600, It.IsAny<CancellationToken>()))
            .Callback<string, object?, int, CancellationToken>((_, value, _, _) => storedValue = value)
            .Returns(Task.CompletedTask);

        var service = new PodOpinionService(
            podService.Object,
            dhtClient.Object,
            ed25519,
            NullLogger<PodOpinionService>.Instance);

        var result = await service.PublishOpinionAsync("pod-1", opinion);

        Assert.True(result.Success);
        var storedOpinions = Assert.IsType<List<PodVariantOpinion>>(storedValue);
        var storedOpinion = Assert.Single(storedOpinions);
        Assert.Equal(opinion.ContentId, storedOpinion.ContentId);
        Assert.Equal(opinion.Signature, storedOpinion.Signature);
    }

    [Fact]
    public async Task GetOpinionsAsync_ReturnsSnapshot_WhenCacheMutatesLater()
    {
        using var ed25519 = new Ed25519Signer();
        var keyPair = ed25519.GenerateKeyPair();
        var publicKeyBase64 = Convert.ToBase64String(keyPair.PublicKey);
        var initialOpinion = CreateSignedOpinion(ed25519, keyPair.PrivateKey, "pod-1", "peer-1", variantHash: "variant-a", note: "first");
        var newOpinion = CreateSignedOpinion(ed25519, keyPair.PrivateKey, "pod-1", "peer-1", variantHash: "variant-b", note: "second");
        var expectedKey = "pod:pod-1:opinions:content:audio:track:track-1";

        var podService = CreatePodServiceMock(publicKeyBase64);
        var dhtClient = new Mock<IMeshDhtClient>();
        dhtClient.SetupSequence(x => x.GetAsync<List<PodVariantOpinion>>(expectedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodVariantOpinion> { initialOpinion })
            .ReturnsAsync(new List<PodVariantOpinion> { initialOpinion });
        dhtClient.Setup(x => x.PutAsync(expectedKey, It.IsAny<object?>(), 3600, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new PodOpinionService(
            podService.Object,
            dhtClient.Object,
            ed25519,
            NullLogger<PodOpinionService>.Instance);

        var cachedSnapshot = await service.GetOpinionsAsync("pod-1", initialOpinion.ContentId);
        var publishResult = await service.PublishOpinionAsync("pod-1", newOpinion);
        var currentOpinions = await service.GetOpinionsAsync("pod-1", initialOpinion.ContentId);

        Assert.True(publishResult.Success);
        Assert.Single(cachedSnapshot);
        Assert.Equal("variant-a", cachedSnapshot[0].VariantHash);
        Assert.Equal(2, currentOpinions.Count);
    }

    [Fact]
    public async Task GetAggregatedOpinionsAsync_NormalizesWeightedAverageByTotalWeight()
    {
        var podService = new Mock<IPodService>();
        var opinionService = new Mock<IPodOpinionService>();
        var messageStorage = new Mock<IPodMessageStorage>();

        podService.Setup(x => x.GetPodAsync("pod-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Pod
            {
                PodId = "pod-1",
                Channels = new List<PodChannel> { new() { ChannelId = "general", Name = "general" } },
            });

        opinionService.Setup(x => x.GetOpinionsAsync("pod-1", "content:audio:track:track-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodVariantOpinion>
            {
                new() { ContentId = "content:audio:track:track-1", VariantHash = "variant-a", Score = 10, SenderPeerId = "peer-1", Signature = "ed25519:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==" },
                new() { ContentId = "content:audio:track:track-1", VariantHash = "variant-a", Score = 0, SenderPeerId = "peer-2", Signature = "ed25519:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==" },
            });

        var aggregator = new PodOpinionAggregator(
            podService.Object,
            opinionService.Object,
            messageStorage.Object,
            NullLogger<PodOpinionAggregator>.Instance);

        SeedAffinityCache(
            aggregator,
            "pod-1",
            new MemberAffinity("peer-1", 1.0, 0, 0, TimeSpan.Zero, DateTimeOffset.UtcNow, 0.5, Array.Empty<string>()),
            new MemberAffinity("peer-2", 0.25, 0, 0, TimeSpan.Zero, DateTimeOffset.UtcNow, 0.5, Array.Empty<string>()));

        var result = await aggregator.GetAggregatedOpinionsAsync("pod-1", "content:audio:track:track-1");

        Assert.Equal(8.0, result.WeightedAverageScore, 6);
        Assert.Single(result.VariantAggregates);
        Assert.Equal(8.0, result.VariantAggregates[0].WeightedAverageScore, 6);
    }

    [Fact]
    public async Task UpdateMemberAffinitiesAsync_WhenDependencyThrows_ReturnsSanitizedErrorMessage()
    {
        var podService = new Mock<IPodService>();
        var opinionService = new Mock<IPodOpinionService>();
        var messageStorage = new Mock<IPodMessageStorage>();

        var aggregator = new PodOpinionAggregator(
            podService.Object,
            opinionService.Object,
            messageStorage.Object,
            NullLogger<PodOpinionAggregator>.Instance);

        var affinityCacheField = typeof(PodOpinionAggregator).GetField("_affinityCache", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(affinityCacheField);
        affinityCacheField!.SetValue(aggregator, null);

        var result = await aggregator.UpdateMemberAffinitiesAsync("pod-1");

        Assert.False(result.Success);
        Assert.Equal("Failed to update member affinities", result.ErrorMessage);
        Assert.DoesNotContain("Object reference", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static Mock<IPodService> CreatePodServiceMock(string publicKeyBase64)
    {
        var podService = new Mock<IPodService>();
        podService.Setup(x => x.GetPodAsync("pod-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Pod { PodId = "pod-1" });
        podService.Setup(x => x.GetMembersAsync("pod-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodMember>
            {
                new() { PeerId = "peer-1", PublicKey = publicKeyBase64 },
            });
        return podService;
    }

    private static PodVariantOpinion CreateSignedOpinion(
        Ed25519Signer ed25519,
        byte[] privateKey,
        string podId,
        string senderPeerId,
        string contentId = "content:audio:track:track-1",
        string variantHash = "variant-1",
        double score = 8.5,
        string note = "good")
    {
        var opinion = new PodVariantOpinion
        {
            ContentId = contentId,
            VariantHash = variantHash,
            Score = score,
            Note = note,
            SenderPeerId = senderPeerId,
        };

        var payload = CreateCanonicalOpinionPayload(podId, opinion);
        var signature = ed25519.Sign(Encoding.UTF8.GetBytes(payload), privateKey);
        opinion.Signature = "ed25519:" + Convert.ToBase64String(signature);
        return opinion;
    }

    private static string CreateCanonicalOpinionPayload(string podId, PodVariantOpinion opinion)
    {
        var noteHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(opinion.Note ?? string.Empty)));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"1|{podId}|{opinion.ContentId}|{opinion.VariantHash}|{opinion.SenderPeerId}|{opinion.Score:G17}|{noteHash}");
    }

    private static void SeedAffinityCache(PodOpinionAggregator aggregator, string podId, params MemberAffinity[] affinities)
    {
        var field = typeof(PodOpinionAggregator).GetField("_affinityCache", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var cache = Assert.IsType<ConcurrentDictionary<string, ConcurrentDictionary<string, MemberAffinity>>>(field!.GetValue(aggregator));
        var podCache = new ConcurrentDictionary<string, MemberAffinity>();
        foreach (var affinity in affinities)
        {
            podCache[affinity.PeerId] = affinity;
        }

        cache[podId] = podCache;
    }
}
