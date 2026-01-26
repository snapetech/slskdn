// <copyright file="SearchResponseMerger.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Search;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Merges and deduplicates Soulseek and mesh search responses by (Username, Filename, Size).
/// </summary>
public static class SearchResponseMerger
{
    /// <summary>
    /// Deduplicates Soulseek and mesh responses by (Username, Filename, Size). Keeps first occurrence.
    /// </summary>
    public static List<Response> Deduplicate(IEnumerable<Response> soulseekResponses, IReadOnlyList<Response> meshResponses)
    {
        var seen = new HashSet<(string Username, string Filename, long Size)>();
        var merged = new List<Response>();

        foreach (var r in soulseekResponses.Concat(meshResponses))
        {
            var keptFiles = r.Files?.Where(f => seen.Add((r.Username, f.Filename, f.Size))).ToList() ?? new List<File>();
            var keptLocked = r.LockedFiles?.Where(f => seen.Add((r.Username, f.Filename, f.Size))).ToList() ?? new List<File>();
            if (keptFiles.Count > 0 || keptLocked.Count > 0)
            {
                merged.Add(new Response
                {
                    Username = r.Username,
                    Token = r.Token,
                    HasFreeUploadSlot = r.HasFreeUploadSlot,
                    UploadSpeed = r.UploadSpeed,
                    QueueLength = r.QueueLength,
                    FileCount = keptFiles.Count,
                    Files = keptFiles,
                    LockedFileCount = keptLocked.Count,
                    LockedFiles = keptLocked,
                });
            }
        }

        return merged;
    }
}
