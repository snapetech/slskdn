// <copyright file="MusicBrainzOverlayServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Integrations.MusicBrainz;

using Microsoft.Extensions.Logging.Abstractions;
using slskd.Integrations.MusicBrainz.Models;
using slskd.Integrations.MusicBrainz.Overlay;
using slskd.SocialFederation;

public sealed class MusicBrainzOverlayServiceTests
{
    [Fact]
    public async Task StoreAsync_RejectsEditWithoutEvidence()
    {
        var service = CreateService();
        var edit = CreateTitleEdit("edit-1", "release-group-1", "Corrected Title");
        edit.Evidence.Clear();
        Sign(edit);

        var result = await service.StoreAsync(edit);

        Assert.False(result.IsValid);
        Assert.Contains("At least one evidence item is required.", result.Errors);
    }

    [Fact]
    public async Task StoreAsync_RejectsTamperedSignatureHash()
    {
        var service = CreateService();
        var edit = CreateTitleEdit("edit-1", "release-group-1", "Corrected Title");
        edit.Value = "Tampered Title";

        var result = await service.StoreAsync(edit);

        Assert.False(result.IsValid);
        Assert.Contains("Signature payload hash does not match edit contents.", result.Errors);
    }

    [Fact]
    public async Task ApplyToArtistReleaseGraphAsync_AppliesOverlayWithoutMutatingOriginal()
    {
        var service = CreateService();
        var graph = CreateGraph();
        await service.StoreAsync(CreateTitleEdit("edit-1", "release-group-1", "Corrected Group Title"));
        await service.StoreAsync(CreateReleaseTitleEdit("edit-2", "release-1", "Corrected Release Title"));

        var application = await service.ApplyToArtistReleaseGraphAsync(graph);

        Assert.Equal("Original Group Title", graph.ReleaseGroups[0].Title);
        Assert.Equal("Original Release Title", graph.ReleaseGroups[0].Releases[0].Title);
        Assert.Equal("Original Group Title", application.Original.ReleaseGroups[0].Title);
        Assert.Equal("Corrected Group Title", application.Effective.ReleaseGroups[0].Title);
        Assert.Equal("Corrected Release Title", application.Effective.ReleaseGroups[0].Releases[0].Title);
        Assert.Equal(2, application.Provenance.Count);
    }

    [Fact]
    public async Task ApplyToArtistReleaseGraphAsync_AppliesEditsDeterministically()
    {
        var service = CreateService();
        var graph = CreateGraph();
        await service.StoreAsync(CreateTitleEdit("edit-b", "release-group-1", "Second Title", new DateTimeOffset(2026, 4, 30, 19, 40, 2, TimeSpan.Zero)));
        await service.StoreAsync(CreateTitleEdit("edit-a", "release-group-1", "First Title", new DateTimeOffset(2026, 4, 30, 19, 40, 1, TimeSpan.Zero)));

        var application = await service.ApplyToArtistReleaseGraphAsync(graph);

        Assert.Equal("Second Title", application.Effective.ReleaseGroups[0].Title);
        Assert.Collection(
            application.Provenance,
            first => Assert.Equal("edit-a", first.EditId),
            second => Assert.Equal("edit-b", second.EditId));
    }

    [Fact]
    public async Task GetExportReviewAsync_ReturnsManualSubmissionPackage()
    {
        var service = CreateService();
        await service.StoreAsync(CreateTitleEdit("edit-1", "release-group-1", "Corrected Group Title"));

        var review = await service.GetExportReviewAsync("edit-1");

        Assert.NotNull(review);
        Assert.Equal("ReleaseGroup:release-group-1", review.UpstreamTarget);
        Assert.Equal("title => Corrected Group Title", review.ProposedChange);
        Assert.True(review.CanApproveExport);
        Assert.Equal("Overlay edit can be reviewed for manual upstream MusicBrainz submission.", review.ReviewReason);
        Assert.Single(review.Evidence);
    }

    [Fact]
    public async Task ApproveExportAsync_RecordsLocalApprovalWithoutChangingOverlay()
    {
        var service = CreateService();
        await service.StoreAsync(CreateTitleEdit("edit-1", "release-group-1", "Corrected Group Title"));

        var result = await service.ApproveExportAsync(
            "edit-1",
            new MusicBrainzOverlayExportApprovalRequest
            {
                ApprovedBy = "actor:operator",
                Note = "ready for manual MB edit",
            });
        var review = await service.GetExportReviewAsync("edit-1");

        Assert.True(result.IsApproved);
        Assert.NotNull(result.Decision);
        Assert.Equal("actor:operator", result.Decision.ApprovedBy);
        Assert.Equal("ReleaseGroup:release-group-1", result.Decision.UpstreamTarget);
        Assert.NotNull(review);
        Assert.NotNull(review.Decision);
        Assert.False(review.CanApproveExport);
        Assert.Equal("Upstream export has already been approved locally.", review.ReviewReason);
    }

    [Fact]
    public async Task ApproveExportAsync_IsIdempotentForExistingApproval()
    {
        var service = CreateService();
        await service.StoreAsync(CreateTitleEdit("edit-1", "release-group-1", "Corrected Group Title"));
        var first = await service.ApproveExportAsync(
            "edit-1",
            new MusicBrainzOverlayExportApprovalRequest
            {
                ApprovedBy = "actor:operator",
            });

        var second = await service.ApproveExportAsync(
            "edit-1",
            new MusicBrainzOverlayExportApprovalRequest
            {
                ApprovedBy = "actor:other",
            });

        Assert.True(second.IsApproved);
        Assert.NotNull(first.Decision);
        Assert.NotNull(second.Decision);
        Assert.Equal(first.Decision.Id, second.Decision.Id);
        Assert.Equal("actor:operator", second.Decision.ApprovedBy);
    }

    [Fact]
    public async Task ApproveExportAsync_RejectsUnsafeApproverIdentifier()
    {
        var service = CreateService();
        await service.StoreAsync(CreateTitleEdit("edit-1", "release-group-1", "Corrected Group Title"));

        var result = await service.ApproveExportAsync(
            "edit-1",
            new MusicBrainzOverlayExportApprovalRequest
            {
                ApprovedBy = "/home/user/private-operator",
            });

        Assert.False(result.IsApproved);
        Assert.Contains("Approved-by identifier must be opaque and safe.", result.Errors);
    }

    private static MusicBrainzOverlayService CreateService()
    {
        return new MusicBrainzOverlayService(NullLogger<MusicBrainzOverlayService>.Instance);
    }

    private static ArtistReleaseGraph CreateGraph()
    {
        return new ArtistReleaseGraph
        {
            ArtistId = "artist-1",
            Name = "Original Artist",
            ReleaseGroups = new List<ReleaseGroup>
            {
                new()
                {
                    ReleaseGroupId = "release-group-1",
                    Title = "Original Group Title",
                    Type = ReleaseGroupType.Album,
                    Releases = new List<Release>
                    {
                        new()
                        {
                            ReleaseId = "release-1",
                            Title = "Original Release Title",
                        },
                    },
                },
            },
        };
    }

    private static MusicBrainzOverlayEdit CreateTitleEdit(
        string id,
        string targetId,
        string value,
        DateTimeOffset? createdAt = null)
    {
        var edit = new MusicBrainzOverlayEdit
        {
            Id = id,
            Type = MusicBrainzOverlayEditType.TitleCorrection,
            TargetType = MusicBrainzOverlayTargetType.ReleaseGroup,
            TargetId = targetId,
            Field = "title",
            Value = value,
            SourceScope = "realm:scene-realm",
            CreatedAt = createdAt ?? new DateTimeOffset(2026, 4, 30, 19, 40, 0, TimeSpan.Zero),
            Evidence = new List<MusicBrainzOverlayEvidence>
            {
                new()
                {
                    Type = MusicBrainzOverlayEvidenceType.WorkRef,
                    Reference = "workref:rare-track",
                    WorkRef = new WorkRef
                    {
                        Id = "https://realm.example/works/rare-track",
                        Domain = "music",
                        Title = "Rare Track",
                        Creator = "Scene Artist",
                    },
                },
            },
        };
        Sign(edit);
        return edit;
    }

    private static MusicBrainzOverlayEdit CreateReleaseTitleEdit(string id, string targetId, string value)
    {
        var edit = CreateTitleEdit(id, targetId, value);
        edit.TargetType = MusicBrainzOverlayTargetType.Release;
        Sign(edit);
        return edit;
    }

    private static void Sign(MusicBrainzOverlayEdit edit)
    {
        edit.Signature = new MusicBrainzOverlaySignature
        {
            Signer = "local-user",
            PayloadHash = edit.ComputePayloadHash(),
            Value = "signed-local-edit",
        };
    }
}
