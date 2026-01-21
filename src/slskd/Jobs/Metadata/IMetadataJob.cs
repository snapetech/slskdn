// <copyright file="IMetadataJob.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Threading;
using System.Threading.Tasks;

namespace slskd.Jobs.Metadata;

/// <summary>
/// Base abstraction for metadata-related jobs (MB backfill, discography, repair, stress tests).
/// </summary>
public interface IMetadataJob
{
    string JobId { get; }
    string Kind { get; }
    Task ExecuteAsync(CancellationToken ct = default);
}
