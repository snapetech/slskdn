// <copyright file="ISourceFeedImportService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SourceFeeds;

public interface ISourceFeedImportService
{
    Task<SourceFeedImportResult> PreviewAsync(
        SourceFeedImportRequest request,
        CancellationToken cancellationToken = default);
}
