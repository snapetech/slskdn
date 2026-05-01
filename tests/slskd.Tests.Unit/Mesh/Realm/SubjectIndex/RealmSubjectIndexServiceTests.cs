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
    public async Task GetConflictReportAsync_FlagsExternalIdConflictForSameSubject()
    {
        var service = CreateService();
        var first = CreateIndex();
        var second = CreateIndex();
        second.Id = "scene-index-alt";
        second.Revision = 4;
        second.Entries[0].ExternalIds["musicbrainz:recording"] = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        second.Entries[0].WorkRef.ExternalIds["musicbrainz"] = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        Sign(second);

        await service.StoreAsync(first);
        await service.StoreAsync(second);

        var report = await service.GetConflictReportAsync("scene-realm");

        Assert.True(report.HasConflicts);
        Assert.Equal(2, report.IndexCount);
        Assert.Equal(2, report.EntryCount);
        var conflict = Assert.Single(report.Conflicts, item => item.Type == "external-id" && item.Key == "musicbrainz:recording");
        Assert.Equal("subject:rare-track", conflict.SubjectId);
        Assert.Equal(2, conflict.Values.Count);
        Assert.All(conflict.Values, value => Assert.StartsWith("realm:scene-realm:subject-index:", value.Provenance));
    }

    [Fact]
    public async Task GetConflictReportAsync_FlagsRecordingMappedToMultipleSubjects()
    {
        var service = CreateService();
        var first = CreateIndex();
        var second = CreateIndex();
        second.Id = "scene-index-alt";
        second.Revision = 4;
        second.Entries[0].SubjectId = "subject:rare-track-remaster";
        second.Entries[0].Aliases = new List<string> { "Rare Track remaster" };
        Sign(second);

        await service.StoreAsync(first);
        await service.StoreAsync(second);

        var report = await service.GetConflictReportAsync("scene-realm");

        var conflict = Assert.Single(report.Conflicts, item => item.Type == "recording-subject");
        Assert.Equal("12345678-1234-1234-1234-1234567890ab", conflict.Key);
        Assert.Equal(new[] { "subject:rare-track", "subject:rare-track-remaster" }, conflict.Values.Select(value => value.Value).Order());
    }

    [Fact]
    public async Task GetConflictReportAsync_FlagsConflictingWorkIdentityForSameSubject()
    {
        var service = CreateService();
        var first = CreateIndex();
        var second = CreateIndex();
        second.Id = "scene-index-alt";
        second.Revision = 4;
        second.Entries[0].WorkRef.Title = "Rare Track (Live)";
        Sign(second);

        await service.StoreAsync(first);
        await service.StoreAsync(second);

        var report = await service.GetConflictReportAsync("scene-realm");

        var conflict = Assert.Single(report.Conflicts, item => item.Type == "workref-identity");
        Assert.Equal("subject:rare-track", conflict.SubjectId);
        Assert.Contains(conflict.Values, value => value.Value == "Rare Track|Scene Artist");
        Assert.Contains(conflict.Values, value => value.Value == "Rare Track (Live)|Scene Artist");
    }

    [Fact]
    public async Task GetConflictReportAsync_FlagsAliasMappedToMultipleSubjects()
    {
        var service = CreateService();
        var first = CreateIndex();
        var second = CreateIndex();
        second.Id = "scene-index-alt";
        second.Revision = 4;
        second.Entries[0].SubjectId = "subject:other-track";
        second.Entries[0].WorkRef.Title = "Other Track";
        Sign(second);

        await service.StoreAsync(first);
        await service.StoreAsync(second);

        var report = await service.GetConflictReportAsync("scene-realm");

        var conflict = Assert.Single(report.Conflicts, item => item.Type == "alias-subject");
        Assert.Equal("Rare Track (dubplate)", conflict.Key);
        Assert.Equal(new[] { "subject:other-track", "subject:rare-track" }, conflict.Values.Select(value => value.Value).Order());
    }

    [Fact]
    public async Task SetAuthorityEnabledAsync_DisablesIndexForResolutionsAndConflicts()
    {
        var service = CreateService();
        var first = CreateIndex();
        var second = CreateIndex();
        second.Id = "scene-index-alt";
        second.Revision = 4;
        second.Entries[0].SubjectId = "subject:other-track";
        second.Entries[0].WorkRef.Title = "Other Track";
        Sign(second);
        await service.StoreAsync(first);
        await service.StoreAsync(second);

        var decision = await service.SetAuthorityEnabledAsync(
            "scene-realm",
            "scene-index-alt",
            new RealmSubjectIndexAuthorityDecisionRequest
            {
                Enabled = false,
                DecidedBy = "local-curator",
                Note = "conflicting alias evidence",
            });
        var resolutions = await service.ResolveByRecordingIdAsync("12345678-1234-1234-1234-1234567890ab");
        var report = await service.GetConflictReportAsync("scene-realm");

        Assert.True(decision.IsAccepted);
        Assert.False(decision.Enabled);
        Assert.Single(resolutions);
        Assert.Equal("scene-index", resolutions[0].IndexId);
        Assert.False(report.HasConflicts);
        Assert.Equal(1, report.IndexCount);
        Assert.Equal(1, report.DisabledAuthorityCount);
        Assert.Equal(1, report.EntryCount);
    }

    [Fact]
    public async Task SetAuthorityEnabledAsync_CanReenableIndexAuthority()
    {
        var service = CreateService();
        var first = CreateIndex();
        var second = CreateIndex();
        second.Id = "scene-index-alt";
        second.Revision = 4;
        second.Entries[0].SubjectId = "subject:other-track";
        second.Entries[0].WorkRef.Title = "Other Track";
        Sign(second);
        await service.StoreAsync(first);
        await service.StoreAsync(second);
        await service.SetAuthorityEnabledAsync(
            "scene-realm",
            "scene-index-alt",
            new RealmSubjectIndexAuthorityDecisionRequest
            {
                Enabled = false,
                DecidedBy = "local-curator",
            });

        var decision = await service.SetAuthorityEnabledAsync(
            "scene-realm",
            "scene-index-alt",
            new RealmSubjectIndexAuthorityDecisionRequest
            {
                Enabled = true,
                DecidedBy = "local-curator",
            });
        var report = await service.GetConflictReportAsync("scene-realm");

        Assert.True(decision.IsAccepted);
        Assert.True(decision.Enabled);
        Assert.True(report.HasConflicts);
        Assert.Equal(2, report.IndexCount);
        Assert.Equal(0, report.DisabledAuthorityCount);
    }

    [Fact]
    public async Task SetAuthorityEnabledAsync_RejectsUnsafeActorAndMissingIndex()
    {
        var service = CreateService();
        var index = CreateIndex();
        await service.StoreAsync(index);

        var decision = await service.SetAuthorityEnabledAsync(
            "scene-realm",
            "missing-index",
            new RealmSubjectIndexAuthorityDecisionRequest
            {
                Enabled = false,
                DecidedBy = "https://private.example/user",
            });
        var decisions = await service.GetAuthorityDecisionsForRealmAsync("scene-realm");

        Assert.False(decision.IsAccepted);
        Assert.Contains("Index authority was not found.", decision.Errors);
        Assert.Contains("Decided-by identifier must be opaque and safe.", decision.Errors);
        Assert.Empty(decisions);
    }

    [Fact]
    public async Task GetAuthorityDecisionsForRealmAsync_ReturnsStoredDecisions()
    {
        var service = CreateService();
        var index = CreateIndex();
        await service.StoreAsync(index);
        await service.SetAuthorityEnabledAsync(
            "scene-realm",
            "scene-index",
            new RealmSubjectIndexAuthorityDecisionRequest
            {
                Enabled = false,
                DecidedBy = "local-curator",
                Note = "prefer another authority",
            });

        var decisions = await service.GetAuthorityDecisionsForRealmAsync("scene-realm");

        var decision = Assert.Single(decisions);
        Assert.Equal("scene-index", decision.IndexId);
        Assert.False(decision.Enabled);
        Assert.Equal("prefer another authority", decision.Note);
    }

    [Fact]
    public async Task Constructor_LoadsPersistedIndexesAndAuthorityDecisions()
    {
        var storagePath = CreateStoragePath();
        var service = CreateService(storagePath);
        var first = CreateIndex();
        var second = CreateIndex();
        second.Id = "scene-index-alt";
        second.Revision = 4;
        second.Entries[0].SubjectId = "subject:other-track";
        second.Entries[0].WorkRef.Title = "Other Track";
        Sign(second);
        await service.StoreAsync(first);
        await service.StoreAsync(second);
        await service.SetAuthorityEnabledAsync(
            "scene-realm",
            "scene-index-alt",
            new RealmSubjectIndexAuthorityDecisionRequest
            {
                Enabled = false,
                DecidedBy = "local-curator",
                Note = "restart-safe disable",
            });

        var reloaded = CreateService(storagePath);
        var resolutions = await reloaded.ResolveByRecordingIdAsync("12345678-1234-1234-1234-1234567890ab");
        var decisions = await reloaded.GetAuthorityDecisionsForRealmAsync("scene-realm");
        var report = await reloaded.GetConflictReportAsync("scene-realm");

        Assert.Single(resolutions);
        Assert.Equal("scene-index", resolutions[0].IndexId);
        var decision = Assert.Single(decisions);
        Assert.False(decision.Enabled);
        Assert.Equal("restart-safe disable", decision.Note);
        Assert.Equal(1, report.DisabledAuthorityCount);
    }

    [Fact]
    public async Task Constructor_LoadsPersistedProposalReviewState()
    {
        var storagePath = CreateStoragePath();
        var service = CreateService(storagePath);
        var index = CreateIndex();
        var proposal = await service.ProposeAsync(index, "local-curator");
        await service.ReviewProposalAsync(proposal.ProposalId, "trusted-root", accept: true, "approved");

        var reloaded = CreateService(storagePath);
        var proposals = await reloaded.GetProposalsForRealmAsync("scene-realm");
        var resolutions = await reloaded.ResolveByRecordingIdAsync("12345678-1234-1234-1234-1234567890ab");

        var reloadedProposal = Assert.Single(proposals);
        Assert.Equal(RealmSubjectIndexProposalStatus.Accepted, reloadedProposal.Status);
        Assert.NotNull(reloadedProposal.Review);
        Assert.Equal("trusted-root", reloadedProposal.Review.ReviewedBy);
        Assert.Single(resolutions);
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
        return CreateService(CreateStoragePath());
    }

    private RealmSubjectIndexService CreateService(string storagePath)
    {
        return new RealmSubjectIndexService(
            _realmService.Object,
            NullLogger<RealmSubjectIndexService>.Instance,
            storagePath,
            _governanceClient.Object);
    }

    private static string CreateStoragePath()
    {
        return Path.Combine(Path.GetTempPath(), "slskdn-subject-index-tests", $"{Guid.NewGuid():N}.json");
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
