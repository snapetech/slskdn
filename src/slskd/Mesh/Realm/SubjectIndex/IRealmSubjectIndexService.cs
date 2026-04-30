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

    Task<IReadOnlyList<RealmSubjectIndexResolution>> ResolveByRecordingIdAsync(
        string recordingId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RealmSubjectIndex>> GetIndexesForRealmAsync(
        string realmId,
        CancellationToken cancellationToken = default);
}
