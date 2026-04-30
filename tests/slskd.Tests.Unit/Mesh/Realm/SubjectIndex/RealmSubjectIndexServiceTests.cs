// <copyright file="RealmSubjectIndexServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Mesh.Realm.SubjectIndex;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Mesh.Governance;
using slskd.Mesh.Realm;
using slskd.Mesh.Realm.SubjectIndex;
using slskd.SocialFederation;

public sealed class RealmSubjectIndexServiceTests
{
    private readonly Mock<IRealmService> _realmService = new();
    private readonly Mock<IRealmAwareGovernanceClient> _governanceClient = new();

    public RealmSubjectIndexServiceTests()
    {
        _realmService.Setup(service => service.IsSameRealm("scene-realm")).Returns(true);
        _realmService.Setup(service => service.IsTrustedGovernanceRoot("trusted-root")).Returns(true);
        _governanceClient
            .Setup(client => client.StoreDocumentForRealmAsync(
                It.IsAny<GovernanceDocument>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task StoreAsync_AcceptsSignedSafeIndex()
    {
        var service = CreateService();
        var index = CreateIndex();

        var validation = await service.StoreAsync(index);

        Assert.True(validation.IsValid);
        var stored = await service.GetIndexesForRealmAsync("scene-realm");
        Assert.Single(stored);
    }

    [Fact]
    public async Task StoreAsync_RejectsUntrustedSigner()
    {
        var service = CreateService();
        var index = CreateIndex();
        index.Signature.Signer = "unknown-root";

        var validation = await service.StoreAsync(index);

        Assert.False(validation.IsValid);
        Assert.Contains("Signature signer is not trusted for this realm.", validation.Errors);
        var stored = await service.GetIndexesForRealmAsync("scene-realm");
        Assert.Empty(stored);
    }

    [Fact]
    public async Task StoreAsync_RejectsTamperedPayloadHash()
    {
        var service = CreateService();
        var index = CreateIndex();
        index.Entries[0].Aliases.Add("tampered alias");

        var validation = await service.StoreAsync(index);

        Assert.False(validation.IsValid);
        Assert.Contains("Signature payload hash does not match index contents.", validation.Errors);
    }

    [Fact]
    public async Task StoreAsync_RejectsUnsafeEvidenceLink()
    {
        var service = CreateService();
        var index = CreateIndex();
        index.Entries[0].EvidenceLinks.Add("file:///home/user/private-note.txt");
        Sign(index);

        var validation = await service.StoreAsync(index);

        Assert.False(validation.IsValid);
        Assert.Contains("Entry 'subject:rare-track' has an unsafe evidence link.", validation.Errors);
    }

    [Fact]
    public async Task ResolveByRecordingIdAsync_ReturnsRealmProvenance()
    {
        var service = CreateService();
        var index = CreateIndex();
        await service.StoreAsync(index);

        var resolutions = await service.ResolveByRecordingIdAsync("12345678-1234-1234-1234-1234567890ab");

        var resolution = Assert.Single(resolutions);
        Assert.Equal("scene-realm", resolution.RealmId);
        Assert.Equal("scene-index", resolution.IndexId);
        Assert.Equal("Rare Track", resolution.Entry.WorkRef.Title);
        Assert.Equal("realm:scene-realm:subject-index:scene-index:r3", resolution.Provenance);
    }

    [Fact]
    public async Task ProposeAsync_StoresGovernanceProposalWithoutPublishingIndex()
    {
        var service = CreateService();
        var index = CreateIndex();

        var proposal = await service.ProposeAsync(index, "local-curator", "ready for review");

        Assert.Equal(RealmSubjectIndexProposalStatus.Pending, proposal.Status);
        Assert.Empty(proposal.Errors);
        Assert.Equal("scene-realm:scene-index:r3", proposal.ProposalId);
        var resolutions = await service.ResolveByRecordingIdAsync("12345678-1234-1234-1234-1234567890ab");
        Assert.Empty(resolutions);
        _governanceClient.Verify(client => client.StoreDocumentForRealmAsync(
            It.Is<GovernanceDocument>(document =>
                document.Type == "realm-subject-index.proposal" &&
                document.RealmId == "scene-realm" &&
                document.Signer == "trusted-root"),
            "scene-realm",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewProposalAsync_AcceptsProposalAndPublishesIndex()
    {
        var service = CreateService();
        var index = CreateIndex();
        var proposal = await service.ProposeAsync(index, "local-curator");

        var review = await service.ReviewProposalAsync(proposal.ProposalId, "trusted-root", accept: true, "approved");

        Assert.Empty(review.Errors);
        Assert.True(review.Accept);
        var storedProposals = await service.GetProposalsForRealmAsync("scene-realm");
        Assert.Equal(RealmSubjectIndexProposalStatus.Accepted, Assert.Single(storedProposals).Status);
        var resolutions = await service.ResolveByRecordingIdAsync("12345678-1234-1234-1234-1234567890ab");
        Assert.Single(resolutions);
        _governanceClient.Verify(client => client.StoreDocumentForRealmAsync(
            It.Is<GovernanceDocument>(document =>
                document.Type == "realm-subject-index.review" &&
                document.Signer == "trusted-root"),
            "scene-realm",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewProposalAsync_RejectsProposalWithoutPublishingIndex()
    {
        var service = CreateService();
        var index = CreateIndex();
        var proposal = await service.ProposeAsync(index, "local-curator");

        var review = await service.ReviewProposalAsync(proposal.ProposalId, "trusted-root", accept: false, "needs more evidence");

        Assert.Empty(review.Errors);
        Assert.False(review.Accept);
        var storedProposals = await service.GetProposalsForRealmAsync("scene-realm");
        Assert.Equal(RealmSubjectIndexProposalStatus.Rejected, Assert.Single(storedProposals).Status);
        var resolutions = await service.ResolveByRecordingIdAsync("12345678-1234-1234-1234-1234567890ab");
        Assert.Empty(resolutions);
    }

    [Fact]
    public async Task ReviewProposalAsync_RejectsUntrustedReviewer()
    {
        var service = CreateService();
        var index = CreateIndex();
        var proposal = await service.ProposeAsync(index, "local-curator");

        var review = await service.ReviewProposalAsync(proposal.ProposalId, "unknown-root", accept: true);

        Assert.Contains("Reviewer is not trusted for this realm.", review.Errors);
        var resolutions = await service.ResolveByRecordingIdAsync("12345678-1234-1234-1234-1234567890ab");
        Assert.Empty(resolutions);
    }

    [Fact]
    public async Task ProposeAsync_RejectsUnsafeProposerWithoutGovernanceDocument()
    {
        var service = CreateService();
        var index = CreateIndex();

        var proposal = await service.ProposeAsync(index, "https://private.example/user");

        Assert.Equal(RealmSubjectIndexProposalStatus.Failed, proposal.Status);
        Assert.Contains("Proposed-by identifier must be opaque and safe.", proposal.Errors);
        _governanceClient.Verify(client => client.StoreDocumentForRealmAsync(
            It.IsAny<GovernanceDocument>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private RealmSubjectIndexService CreateService()
    {
        return new RealmSubjectIndexService(
            _realmService.Object,
            NullLogger<RealmSubjectIndexService>.Instance,
            _governanceClient.Object);
    }

    private static RealmSubjectIndex CreateIndex()
    {
        var index = new RealmSubjectIndex
        {
            Id = "scene-index",
            RealmId = "scene-realm",
            SubjectNamespace = "music:rare-scene",
            Revision = 3,
            PublishedAt = new DateTimeOffset(2026, 4, 30, 19, 30, 0, TimeSpan.Zero),
            Entries = new List<RealmSubjectIndexEntry>
            {
                new()
                {
                    SubjectId = "subject:rare-track",
                    WorkRef = new WorkRef
                    {
                        Id = "https://realm.example/works/rare-track",
                        Domain = "music",
                        Title = "Rare Track",
                        Creator = "Scene Artist",
                        ExternalIds = new Dictionary<string, string>
                        {
                            ["musicbrainz"] = "12345678-1234-1234-1234-1234567890ab",
                        },
                    },
                    ExternalIds = new Dictionary<string, string>
                    {
                        ["musicbrainz:recording"] = "12345678-1234-1234-1234-1234567890ab",
                    },
                    Aliases = new List<string> { "Rare Track (dubplate)" },
                    EvidenceLinks = new List<string> { "https://musicbrainz.org/recording/12345678-1234-1234-1234-1234567890ab" },
                    Note = "Realm-curated alias for a niche scene recording.",
                },
            },
        };
        Sign(index);
        return index;
    }

    private static void Sign(RealmSubjectIndex index)
    {
        index.Signature = new RealmSubjectIndexSignature
        {
            Signer = "trusted-root",
            PayloadHash = index.ComputePayloadHash(),
            Value = "signed-by-trusted-root",
        };
    }
}
