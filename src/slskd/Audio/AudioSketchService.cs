namespace slskd.Audio
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Security.Cryptography;
    using Microsoft.Extensions.Options;
    using Serilog;

    /// <summary>
    ///     Generates a short audio sketch hash from downsampled PCM (mono 4 kHz, ~12s).
    /// </summary>
    public class AudioSketchService
    {
        private const int TargetSampleRate = 4000;
        private const int TargetChannels = 1;
        private const int TargetDurationSeconds = 12;

        private readonly ILogger log = Log.ForContext<AudioSketchService>();
        private readonly IOptionsMonitor<slskd.Options> optionsMonitor;

        public AudioSketchService(IOptionsMonitor<slskd.Options> optionsMonitor)
        {
            this.optionsMonitor = optionsMonitor;
        }

        /// <summary>
        ///     Computes a short hash over downsampled PCM for cross-codec matching.
        /// </summary>
        /// <param name="filePath">Audio file path.</param>
        /// <returns>Hex-encoded hash or null on failure.</returns>
        public string ComputeSketchHash(string filePath)
        {
            var ffmpegPath = optionsMonitor.CurrentValue.Integration.Chromaprint.FfmpegPath;
            if (string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath))
            {
                log.Warning("[AudioSketch] ffmpeg not configured or missing: {Path}", ffmpegPath);
                return null;
            }

            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
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
                psi.ArgumentList.Add(TargetDurationSeconds.ToString());
                psi.ArgumentList.Add("-i");
                psi.ArgumentList.Add(filePath);
                psi.ArgumentList.Add("-f");
                psi.ArgumentList.Add("s16le");
                psi.ArgumentList.Add("-acodec");
                psi.ArgumentList.Add("pcm_s16le");
                psi.ArgumentList.Add("-ar");
                psi.ArgumentList.Add(TargetSampleRate.ToString());
                psi.ArgumentList.Add("-ac");
                psi.ArgumentList.Add(TargetChannels.ToString());
                psi.ArgumentList.Add("pipe:1");

                using var process = new Process { StartInfo = psi };
                process.Start();

                using var sha = SHA256.Create();
                var buffer = new byte[8192];

                while (true)
                {
                    var read = process.StandardOutput.BaseStream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        break;
                    }

                    sha.TransformBlock(buffer, 0, read, null, 0);
                }

                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    log.Warning("[AudioSketch] ffmpeg exited with code {Code} for {File}: {Err}", process.ExitCode, filePath, stderr.Trim());
                }

                return BitConverter.ToString(sha.Hash!).Replace("-", string.Empty).ToLowerInvariant();
            }
            catch (Exception ex)
            {
                log.Warning(ex, "[AudioSketch] Failed to compute sketch hash for {File}", filePath);
                return null;
            }
        }
    }
}
