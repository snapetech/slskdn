// <copyright file="IRealmSubjectIndexService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Mesh.Realm.SubjectIndex;

public interface IRealmSubjectIndexService
{
    Task<RealmSubjectIndexValidationResult> ValidateAsync(
        RealmSubjectIndex index,
        CancellationToken cancellationToken = default);

    Task<RealmSubjectIndexValidationResult> StoreAsync(
        RealmSubjectIndex index,
        CancellationToken cancellationToken = default);

    Task<RealmSubjectIndexProposal> ProposeAsync(
        RealmSubjectIndex index,
        string proposedBy,
        string note = "",
        CancellationToken cancellationToken = default);

    Task<RealmSubjectIndexProposalReview> ReviewProposalAsync(
        string proposalId,
        string reviewedBy,
        bool accept,
        string note = "",
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RealmSubjectIndexProposal>> GetProposalsForRealmAsync(
        string realmId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RealmSubjectIndexResolution>> ResolveByRecordingIdAsync(
        string recordingId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RealmSubjectIndex>> GetIndexesForRealmAsync(
        string realmId,
        CancellationToken cancellationToken = default);
}
