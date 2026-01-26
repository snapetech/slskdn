// <copyright file="SearchResponseMerger.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Search;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Merges and deduplicates Soulseek and mesh search responses using normalized filename and size for deduplication.
/// </summary>
public static class SearchResponseMerger
{
    /// <summary>
    /// Deduplicates Soulseek and mesh responses using (Username, normalized filename, size) as the deduplication key.
    /// Keeps first occurrence of each unique file.
    /// </summary>
    public static List<Response> Deduplicate(IEnumerable<Response> soulseekResponses, IReadOnlyList<Response> meshResponses)
    {
        // Track by (normalized filename, size) for deduplication
        var seenByFilename = new HashSet<(string Username, string NormalizedFilename, long Size)>();
        var merged = new List<Response>();

        string NormalizeFilename(string filename)
        {
            // Normalize: lowercase, normalize path separators, trim whitespace
            return filename?.ToLowerInvariant()
                .Replace('\\', '/')
                .Trim() ?? string.Empty;
        }

        foreach (var r in soulseekResponses.Concat(meshResponses))
        {
            var keptFiles = new List<File>();
            var keptLocked = new List<File>();

            // Process regular files
            if (r.Files != null)
            {
                foreach (var f in r.Files)
                {
                    var normalized = NormalizeFilename(f.Filename);
                    var key = (r.Username ?? string.Empty, normalized, f.Size);
                    if (seenByFilename.Add(key))
                    {
                        keptFiles.Add(f);
                    }
                }
            }

            // Process locked files
            if (r.LockedFiles != null)
            {
                foreach (var f in r.LockedFiles)
                {
                    var normalized = NormalizeFilename(f.Filename);
                    var key = (r.Username ?? string.Empty, normalized, f.Size);
                    if (seenByFilename.Add(key))
                    {
                        keptLocked.Add(f);
                    }
                }
            }

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
