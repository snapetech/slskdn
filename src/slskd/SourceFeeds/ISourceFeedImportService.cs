// <copyright file="ISourceFeedImportService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SourceFeeds;

public interface ISourceFeedImportService
{
    Task<SourceFeedImportResult> PreviewAsync(
        SourceFeedImportRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourceFeedImportHistoryEntry>> GetHistoryAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<SourceFeedImportHistoryEntry?> GetHistoryEntryAsync(
        string importId,
        CancellationToken cancellationToken = default);
}
