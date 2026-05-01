// <copyright file="RealmSubjectIndexesControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Mesh.Realm.SubjectIndex;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.Mesh.Realm.SubjectIndex;
using slskd.Mesh.Realm.SubjectIndex.API;

public sealed class RealmSubjectIndexesControllerTests
{
    [Fact]
    public async Task GetConflicts_ReturnsConflictReportForRealm()
    {
        var service = new Mock<IRealmSubjectIndexService>();
        var report = new RealmSubjectIndexConflictReport
        {
            RealmId = "scene-realm",
            IndexCount = 2,
            EntryCount = 3,
        };
        service
            .Setup(subjectIndexService => subjectIndexService.GetConflictReportAsync("scene-realm", It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        var controller = new RealmSubjectIndexesController(service.Object);

        var response = await controller.GetConflicts(" scene-realm ", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        Assert.Same(report, ok.Value);
    }

    [Fact]
    public async Task ResolveRecording_ReturnsRecordingResolutions()
    {
        var service = new Mock<IRealmSubjectIndexService>();
        var resolutions = new List<RealmSubjectIndexResolution>
        {
            new()
            {
                RealmId = "scene-realm",
                IndexId = "scene-index",
                Revision = 3,
                Provenance = "realm:scene-realm:subject-index:scene-index:r3",
            },
        };
        service
            .Setup(subjectIndexService => subjectIndexService.ResolveByRecordingIdAsync(
                "12345678-1234-1234-1234-1234567890ab",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolutions);
        var controller = new RealmSubjectIndexesController(service.Object);

        var response = await controller.ResolveRecording(" 12345678-1234-1234-1234-1234567890ab ", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        Assert.Same(resolutions, ok.Value);
    }

    [Fact]
    public async Task GetIndexes_ReturnsIndexesForRealm()
    {
        var service = new Mock<IRealmSubjectIndexService>();
        var indexes = new List<RealmSubjectIndex>
        {
            new()
            {
                Id = "scene-index",
                RealmId = "scene-realm",
            },
        };
        service
            .Setup(subjectIndexService => subjectIndexService.GetIndexesForRealmAsync("scene-realm", It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexes);
        var controller = new RealmSubjectIndexesController(service.Object);

        var response = await controller.GetIndexes(" scene-realm ", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        Assert.Same(indexes, ok.Value);
    }

    [Fact]
    public async Task GetAuthorityDecisions_ReturnsDecisionsForRealm()
    {
        var service = new Mock<IRealmSubjectIndexService>();
        var decisions = new List<RealmSubjectIndexAuthorityDecision>
        {
            new()
            {
                RealmId = "scene-realm",
                IndexId = "scene-index",
                Enabled = false,
            },
        };
        service
            .Setup(subjectIndexService => subjectIndexService.GetAuthorityDecisionsForRealmAsync(
                "scene-realm",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(decisions);
        var controller = new RealmSubjectIndexesController(service.Object);

        var response = await controller.GetAuthorityDecisions(" scene-realm ", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        Assert.Same(decisions, ok.Value);
    }

    [Fact]
    public async Task SetAuthorityDecision_ReturnsAcceptedDecision()
    {
        var service = new Mock<IRealmSubjectIndexService>();
        service
            .Setup(subjectIndexService => subjectIndexService.SetAuthorityEnabledAsync(
                "scene-realm",
                "scene-index",
                It.IsAny<RealmSubjectIndexAuthorityDecisionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmSubjectIndexAuthorityDecision
            {
                RealmId = "scene-realm",
                IndexId = "scene-index",
                Enabled = false,
            });
        var controller = new RealmSubjectIndexesController(service.Object);

        var response = await controller.SetAuthorityDecision(
            " scene-realm ",
            " scene-index ",
            new RealmSubjectIndexAuthorityDecisionRequest
            {
                Enabled = false,
                DecidedBy = "local-curator",
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        var decision = Assert.IsType<RealmSubjectIndexAuthorityDecision>(ok.Value);
        Assert.False(decision.Enabled);
    }

    [Fact]
    public async Task SetAuthorityDecision_ReturnsBadRequestForRejectedDecision()
    {
        var service = new Mock<IRealmSubjectIndexService>();
        service
            .Setup(subjectIndexService => subjectIndexService.SetAuthorityEnabledAsync(
                "scene-realm",
                "missing-index",
                It.IsAny<RealmSubjectIndexAuthorityDecisionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmSubjectIndexAuthorityDecision
            {
                RealmId = "scene-realm",
                IndexId = "missing-index",
                Enabled = false,
                Errors = new List<string> { "Index authority was not found." },
            });
        var controller = new RealmSubjectIndexesController(service.Object);

        var response = await controller.SetAuthorityDecision(
            "scene-realm",
            "missing-index",
            new RealmSubjectIndexAuthorityDecisionRequest(),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response);
    }
}
