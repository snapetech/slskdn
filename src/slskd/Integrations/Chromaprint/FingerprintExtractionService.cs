// <copyright file="FingerprintExtractionService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.Chromaprint
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using ChromaprintOptions = slskd.Options.IntegrationOptions.ChromaprintOptions;
    using slskdOptions = slskd.Options;

    /// <summary>
    ///     Runs ffmpeg to decode audio into PCM and feeds the samples to Chromaprint.
    /// </summary>
    public class FingerprintExtractionService : IFingerprintExtractionService
    {
        private readonly ILogger<FingerprintExtractionService> log;
        private readonly IChromaprintService chromaprint;
        private readonly IOptionsMonitor<slskdOptions> optionsMonitor;

        /// <summary>
        ///     Initializes a new instance of the <see cref="FingerprintExtractionService"/> class.
        /// </summary>
        public FingerprintExtractionService(
            IChromaprintService chromaprint,
            IOptionsMonitor<slskdOptions> optionsMonitor,
            ILogger<FingerprintExtractionService> log)
        {
            this.chromaprint = chromaprint;
            this.optionsMonitor = optionsMonitor;
            this.log = log;
        }

        private ChromaprintOptions Options => optionsMonitor.CurrentValue.Integration.Chromaprint;

        /// <inheritdoc />
        public async Task<string?> ExtractFingerprintAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var options = Options;

            if (!options.Enabled)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A file path must be supplied", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Audio file not found", filePath);
            }

            var psi = CreateProcessStartInfo(options, filePath);
            using var process = new Process { StartInfo = psi };

            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            process.Start();

            await using var ms = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                log.LogWarning("ffmpeg exited with code {Code} ({Message}) while decoding {File}", process.ExitCode, stderr.Trim(), filePath);
            }

            var bytes = ms.ToArray();
            if (bytes.Length == 0)
            {
                throw new InvalidOperationException($"ffmpeg produced no PCM output for {filePath}. ffmpeg stderr: {stderr}");
            }

            if (bytes.Length % sizeof(short) != 0)
            {
                var truncated = bytes.Length - (bytes.Length % sizeof(short));
                log.LogWarning("Truncating {FilePath} PCM output to {Truncated} bytes to align to sample boundary", filePath, truncated);
                Array.Resize(ref bytes, truncated);
            }

            var sampleCount = bytes.Length / sizeof(short);
            var samples = new short[sampleCount];
            Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);

            return chromaprint.GenerateFingerprint(samples, options.SampleRate, options.Channels);
        }

        private static ProcessStartInfo CreateProcessStartInfo(ChromaprintOptions options, string filePath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = options.FfmpegPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-nostdin");
            psi.ArgumentList.Add("-loglevel");
            psi.ArgumentList.Add("error");
            psi.ArgumentList.Add("-t");
            psi.ArgumentList.Add(options.DurationSeconds.ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(filePath);
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("s16le");
            psi.ArgumentList.Add("-acodec");
            psi.ArgumentList.Add("pcm_s16le");
            psi.ArgumentList.Add("-ar");
            psi.ArgumentList.Add(options.SampleRate.ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("-ac");
            psi.ArgumentList.Add(options.Channels.ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("pipe:1");
            return psi;
        }
    }
}



