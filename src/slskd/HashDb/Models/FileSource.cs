// <copyright file="FileSource.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.HashDb.Models;

using System;

/// <summary>
///     Represents a source (peer + path) for a given content hash.
///     Multiple peers can have the same file content, enabling multi-source downloads.
/// </summary>
public class FileSource
{
    /// <summary>
    ///     Gets or sets the content hash (SHA256 of first 32KB - links to HashDb.byte_hash).
    /// </summary>
    public string ContentHash { get; set; }

    /// <summary>
    ///     Gets or sets the peer username who has this file.
    /// </summary>
    public string PeerId { get; set; }

    /// <summary>
    ///     Gets or sets the full path on the peer's share.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    ///     Gets or sets the file size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    ///     Gets or sets when this source was first discovered (Unix timestamp).
    /// </summary>
    public long FirstSeen { get; set; }

    /// <summary>
    ///     Gets or sets when this source was last seen/verified (Unix timestamp).
    /// </summary>
    public long LastSeen { get; set; }

    /// <summary>
    ///     Gets or sets the number of successful downloads from this source.
    /// </summary>
    public int DownloadSuccessCount { get; set; }

    /// <summary>
    ///     Gets or sets the number of failed download attempts from this source.
    /// </summary>
    public int DownloadFailCount { get; set; }

    /// <summary>
    ///     Gets or sets the average download speed in bytes per second.
    /// </summary>
    public int? AvgSpeedBps { get; set; }

    /// <summary>
    ///     Gets or sets when the last download was attempted (Unix timestamp).
    /// </summary>
    public long? LastDownloadAt { get; set; }

    /// <summary>
    ///     Gets the first seen time as DateTime.
    /// </summary>
    public DateTime FirstSeenUtc => DateTimeOffset.FromUnixTimeSeconds(FirstSeen).UtcDateTime;

    /// <summary>
    ///     Gets the last seen time as DateTime.
    /// </summary>
    public DateTime LastSeenUtc => DateTimeOffset.FromUnixTimeSeconds(LastSeen).UtcDateTime;

    /// <summary>
    ///     Gets the success rate as a percentage (0-100).
    /// </summary>
    public double SuccessRate
    {
        get
        {
            var total = DownloadSuccessCount + DownloadFailCount;
            return total > 0 ? (double)DownloadSuccessCount / total * 100 : 0;
        }
    }

    /// <summary>
    ///     Gets a quality score for ranking this source (higher is better).
    /// </summary>
    public double QualityScore
    {
        get
        {
            // Combine success rate and speed into a single score
            var successFactor = SuccessRate / 100.0;
            var speedFactor = AvgSpeedBps.HasValue ? Math.Min(AvgSpeedBps.Value / 1_000_000.0, 1.0) : 0.5; // Cap at 1MB/s
            var freshnessFactor = Math.Max(0, 1.0 - (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - LastSeen) / 86400.0 / 30); // Decay over 30 days

            return (successFactor * 0.5) + (speedFactor * 0.3) + (freshnessFactor * 0.2);
        }
    }
}

