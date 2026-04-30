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
