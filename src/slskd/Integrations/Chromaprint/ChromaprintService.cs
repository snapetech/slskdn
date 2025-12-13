// <copyright file="ChromaprintService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.Chromaprint
{
    using System;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using slskd;
    using ChromaprintOptions = slskd.Options.IntegrationOptions.ChromaprintOptions;
    using slskdOptions = slskd.Options;

    /// <summary>
    ///     Wraps the native Chromaprint library to compute fingerprints.
    /// </summary>
    public class ChromaprintService : IChromaprintService
    {
        private readonly ILogger<ChromaprintService> log;
        private readonly IOptionsMonitor<slskdOptions> optionsMonitor;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ChromaprintService"/> class.
        /// </summary>
        public ChromaprintService(IOptionsMonitor<slskdOptions> optionsMonitor, ILogger<ChromaprintService> log)
        {
            this.optionsMonitor = optionsMonitor;
            this.log = log;
        }

        private ChromaprintOptions Options => optionsMonitor.CurrentValue.Integration.Chromaprint;

        /// <inheritdoc />
        public string GenerateFingerprint(ReadOnlySpan<short> samples, int sampleRate, int channels)
        {
            var options = Options;

            if (!options.Enabled)
            {
                throw new InvalidOperationException("Chromaprint integration is disabled.");
            }

            if (samples.Length == 0)
            {
                throw new ArgumentException("At least one sample must be supplied.", nameof(samples));
            }

            using var context = ChromaprintContext.Create((int)options.Algorithm);

            if (!context.Start(sampleRate, channels))
            {
                log.LogError("Chromaprint failed to initialize (rate={Rate} ch={Channels})", sampleRate, channels);
                throw new InvalidOperationException("Chromaprint could not start.");
            }

            if (!context.Feed(samples))
            {
                log.LogError("Chromaprint feed failed (samples={Count})", samples.Length);
                throw new InvalidOperationException("Chromaprint could not process the samples.");
            }

            if (!context.Finish())
            {
                log.LogError("Chromaprint finish failed.");
                throw new InvalidOperationException("Chromaprint failed to finish.");
            }

            var fingerprint = context.GetFingerprint();

            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                log.LogError("Chromaprint returned an empty fingerprint.");
                throw new InvalidOperationException("Chromaprint did not return a fingerprint.");
            }

            return fingerprint;
        }
    }
}


















