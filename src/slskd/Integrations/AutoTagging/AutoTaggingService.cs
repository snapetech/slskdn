// <copyright file="AutoTaggingService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.AutoTagging
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using slskd.Integrations.MusicBrainz.Models;
    using TagLib;

    /// <inheritdoc/>
    public class AutoTaggingService : IAutoTaggingService
    {
        private readonly ILogger<AutoTaggingService> log;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AutoTaggingService"/> class.
        /// </summary>
        public AutoTaggingService(ILogger<AutoTaggingService> log)
        {
            this.log = log;
        }

        /// <inheritdoc/>
        public Task<AutoTagResult?> TagAsync(string filePath, TrackTarget track, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath) || track is null)
            {
                return Task.FromResult<AutoTagResult?>(null);
            }

            try
            {
                using var tagFile = TagLib.File.Create(filePath);

                var changed = false;

                if (!string.IsNullOrWhiteSpace(track.Title) && tagFile.Tag.Title != track.Title)
                {
                    tagFile.Tag.Title = track.Title;
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(track.Artist))
                {
                    var performers = tagFile.Tag.Performers;
                    if (performers == null || performers.Length != 1 || performers[0] != track.Artist)
                    {
                        tagFile.Tag.Performers = new[] { track.Artist };
                        changed = true;
                    }
                }

                if (changed)
                {
                    tagFile.Save();
                }

                return Task.FromResult<AutoTagResult?>(new AutoTagResult(
                    filePath,
                    tagFile.Tag.Title ?? string.Empty,
                    tagFile.Tag.FirstPerformer ?? string.Empty,
                    changed));
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Auto-tagging failed for {File}", filePath);
                return Task.FromResult<AutoTagResult?>(null);
            }
        }
    }
}

