// <copyright file="ContentVerificationService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Transfers.MultiSource
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using Soulseek;
    using slskd.HashDb;
    using slskd.HashDb.Models;
    using slskd.Mesh;
    using slskd.Telemetry;

    /// <summary>
    ///     Service for verifying file content identity across multiple Soulseek sources.
    /// </summary>
    public class ContentVerificationService : IContentVerificationService
    {
        /// <summary>
        ///     Size of verification chunk for non-FLAC files (32KB).
        /// </summary>
        public const int VerificationChunkSize = 32768;

        /// <summary>
        ///     Per-peer-per-day cap on verification probes.
        ///     A probe issues a 32KB read followed by a mid-stream cancel which appears as a failed transfer
        ///     on official Soulseek clients; this limits the visible noise we cause on any single uploader.
        /// </summary>
        public const int MaxProbesPerPeerPerDay = 10;

        private static readonly object ProbeBudgetSyncRoot = new();
        private static readonly Dictionary<string, ProbeBudgetEntry> ProbeBudget = new(StringComparer.OrdinalIgnoreCase);
        private static bool probeBudgetLoaded;

        private static bool TryConsumeProbeBudget(string username)
        {
            lock (ProbeBudgetSyncRoot)
            {
                EnsureProbeBudgetLoaded();

                var today = DateTime.UtcNow.Date;
                if (!ProbeBudget.TryGetValue(username, out var current) || current.Day != today)
                {
                    current = new ProbeBudgetEntry { Day = today, Count = 0 };
                }

                if (current.Count >= MaxProbesPerPeerPerDay)
                {
                    return false;
                }

                ProbeBudget[username] = new ProbeBudgetEntry { Day = today, Count = current.Count + 1 };
                SaveProbeBudget();
                return true;
            }
        }

        private static void EnsureProbeBudgetLoaded()
        {
            if (probeBudgetLoaded)
            {
                return;
            }

            probeBudgetLoaded = true;

            try
            {
                var path = GetProbeBudgetPath();
                if (!System.IO.File.Exists(path))
                {
                    return;
                }

                var entries = JsonSerializer.Deserialize<Dictionary<string, ProbeBudgetEntry>>(System.IO.File.ReadAllText(path));
                if (entries == null)
                {
                    return;
                }

                var today = DateTime.UtcNow.Date;
                foreach (var entry in entries.Where(entry => entry.Value.Day == today))
                {
                    ProbeBudget[entry.Key] = entry.Value;
                }
            }
            catch
            {
                // Probe budgets are best-effort; if the file is unreadable, start a fresh daily budget.
            }
        }

        private static void SaveProbeBudget()
        {
            try
            {
                var path = GetProbeBudgetPath();
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                System.IO.File.WriteAllText(path, JsonSerializer.Serialize(ProbeBudget));
            }
            catch
            {
                // Probe budgets remain enforced in-process if persistence fails.
            }
        }

        private static string GetProbeBudgetPath()
        {
            var appDirectory = string.IsNullOrWhiteSpace(Program.AppDirectory)
                ? Program.DefaultAppDirectory
                : Program.AppDirectory;

            return Path.Combine(appDirectory, "verification-probe-budget.json");
        }

        private sealed class ProbeBudgetEntry
        {
            public DateTime Day { get; set; }

            public int Count { get; set; }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ContentVerificationService"/> class.
        /// </summary>
        /// <param name="soulseekClient">The Soulseek client.</param>
        /// <param name="hashDb">The hash database service (optional).</param>
        /// <param name="meshSync">The mesh sync service (optional).</param>
        public ContentVerificationService(
            ISoulseekClient soulseekClient,
            IHashDbService? hashDb = null,
            IMeshSyncService? meshSync = null)
        {
            Client = soulseekClient;
            HashDb = hashDb;
            MeshSync = meshSync;
        }

        private ISoulseekClient Client { get; }
        private IHashDbService? HashDb { get; }
        private IMeshSyncService? MeshSync { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<ContentVerificationService>();

        /// <summary>
        ///     Attempts to look up a known hash from the local database.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <param name="fileSize">The file size.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The known hash, or null if not found.</returns>
        public async Task<string?> TryGetKnownHashAsync(string filename, long fileSize, CancellationToken cancellationToken = default)
        {
            if (HashDb == null)
            {
                return default;
            }

            try
            {
                var flacKey = HashDbEntry.GenerateFlacKey(filename, fileSize);
                var entry = await HashDb.LookupHashAsync(flacKey, cancellationToken);

                if (entry != null)
                {
                    Log.Debug("[HASHDB] Cache hit for {Filename} ({Size} bytes): {Hash}",
                        Path.GetFileName(filename), fileSize, entry.ByteHash?.Substring(0, 16) + "...");

                    // Increment use count
                    await HashDb.IncrementHashUseCountAsync(flacKey, cancellationToken);
                    return entry.ByteHash;
                }

                Log.Debug("[HASHDB] Cache miss for {Filename} ({Size} bytes)", Path.GetFileName(filename), fileSize);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[HASHDB] Error looking up hash for {Filename}", filename);
            }

            return default;
        }

        /// <summary>
        ///     Stores a verified hash in the database and publishes to mesh.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <param name="fileSize">The file size.</param>
        /// <param name="hash">The SHA256 hash of first 32KB.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task StoreVerifiedHashAsync(string filename, long fileSize, string hash, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(hash))
            {
                return;
            }

            try
            {
                // Store in local hash database
                if (HashDb != null)
                {
                    await HashDb.StoreHashFromVerificationAsync(filename, fileSize, hash, cancellationToken: cancellationToken);
                    Log.Debug("[HASHDB] Stored hash for {Filename}: {Hash}", Path.GetFileName(filename), hash.Substring(0, 16) + "...");
                }

                // Publish to mesh for other slskdn clients
                if (MeshSync != null)
                {
                    var flacKey = HashDbEntry.GenerateFlacKey(filename, fileSize);
                    await MeshSync.PublishHashAsync(flacKey, hash, fileSize, cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[HASHDB] Error storing hash for {Filename}", filename);
            }
        }

        /// <inheritdoc/>
        public async Task<ContentVerificationResult> VerifySourcesAsync(
            ContentVerificationRequest request,
            CancellationToken cancellationToken = default)
        {
            var result = new ContentVerificationResult
            {
                Filename = request.Filename,
                FileSize = request.FileSize,
            };

            // Support both old (CandidateUsernames) and new (CandidateSources) API
            var sourcesToVerify = request.CandidateSources.Count > 0
                ? request.CandidateSources
                : request.CandidateUsernames.ToDictionary(u => u, _ => request.Filename);

            Log.Information(
                "Verifying {Count} sources for {Filename} ({Size} bytes)",
                sourcesToVerify.Count,
                request.Filename,
                request.FileSize);

            // Phase 5 Integration: Try to get known hash from database first
            var knownHash = await TryGetKnownHashAsync(request.Filename, request.FileSize, cancellationToken);
            if (knownHash != null)
            {
                Log.Information("[HASHDB] Using cached hash for {Filename}, will verify {Count} sources against it",
                    Path.GetFileName(request.Filename), sourcesToVerify.Count);
                result.ExpectedHash = knownHash;
            }

            // Skip Soulseek-side probes entirely when the caller already has >=2 mesh-overlay sources.
            // Probing public Soulseek peers in that case adds visible "transfer cancelled" entries to
            // their UIs without changing the outcome (we'll prefer the mesh sources anyway).
            if (request.MeshOverlaySourceCount >= 2)
            {
                foreach (var kvp in sourcesToVerify)
                {
                    Telemetry.SwarmMetrics.SwarmVerificationProbesTotal.WithLabels("soulseek", "skipped_mesh").Inc();
                }

                Log.Information(
                    "[VERIFY] Skipping {Count} Soulseek probes; {MeshCount} mesh-overlay sources already verified",
                    sourcesToVerify.Count,
                    request.MeshOverlaySourceCount);
                SetBestSemanticKey(result);
                return result;
            }

            // Verify all candidates in parallel (skipping any that exceed the per-peer-per-day probe budget).
            var verificationTasks = new List<Task<(string Username, string? Hash, VerificationMethod Method, long TimeMs, string? Error)>>();

            foreach (var kvp in sourcesToVerify)
            {
                if (!TryConsumeProbeBudget(kvp.Key))
                {
                    Telemetry.SwarmMetrics.SwarmVerificationProbesTotal.WithLabels("soulseek", "skipped_budget").Inc();
                    Log.Information(
                        "[VERIFY] Skipping probe for {Username}: per-peer-per-day budget exhausted ({Cap})",
                        kvp.Key,
                        MaxProbesPerPeerPerDay);
                    result.FailedSources.Add(new FailedSource
                    {
                        Username = kvp.Key,
                        Reason = $"Verification probe budget exhausted ({MaxProbesPerPeerPerDay}/day)",
                    });
                    continue;
                }

                verificationTasks.Add(VerifySingleSourceAsync(
                    kvp.Key,       // username
                    kvp.Value,     // each user's specific filename
                    request.FileSize,
                    request.TimeoutMs,
                    cancellationToken));
            }

            var verificationResults = await Task.WhenAll(verificationTasks);

            // Group results by hash
            foreach (var (username, hash, method, timeMs, error) in verificationResults)
            {
                if (error != null || hash == null)
                {
                    result.FailedSources.Add(new FailedSource
                    {
                        Username = username,
                        Reason = error ?? "Verification returned no hash",
                    });

                    continue;
                }

                var entry = await LookupHashDbEntryAsync(sourcesToVerify[username], request.FileSize, cancellationToken).ConfigureAwait(false);
                if (!result.SourcesByHash.TryGetValue(hash, out var sources))
                {
                    sources = new List<VerifiedSource>();
                    result.SourcesByHash[hash] = sources;
                }

                sources.Add(new VerifiedSource
                {
                    Username = username,
                    FullPath = sourcesToVerify[username],  // Store each user's specific path
                    ContentHash = hash,
                    Method = method,
                    VerificationTimeMs = timeMs,
                    MusicBrainzRecordingId = entry?.MusicBrainzId,
                    AudioFingerprint = entry?.AudioFingerprint,
                    CodecProfile = method.ToString(),
                });

                AddSemanticSource(result, sources.Last());
            }

            Log.Information(
                "Verification complete: {HashGroups} hash groups, {Failed} failed, best group has {BestCount} sources",
                result.SourcesByHash.Count,
                result.FailedSources.Count,
                result.BestSources.Count);

            // Phase 5 Integration: Store the best hash in the database for future lookups
            if (result.BestSources.Count > 0)
            {
                var bestHash = result.BestSources.First().ContentHash;
                await StoreVerifiedHashAsync(request.Filename, request.FileSize, bestHash, cancellationToken);
            }

            SetBestSemanticKey(result);
            return result;
        }

        private static void AddSemanticSource(ContentVerificationResult result, VerifiedSource source)
        {
            var semanticKey = ComputeSemanticKey(source);
            if (!result.SourcesBySemanticKey.TryGetValue(semanticKey, out var bucket))
            {
                bucket = new List<VerifiedSource>();
                result.SourcesBySemanticKey[semanticKey] = bucket;
            }

            bucket.Add(source);
        }

        private static string ComputeSemanticKey(VerifiedSource source)
        {
            var keyParts = new List<string>();
            keyParts.Add(string.IsNullOrWhiteSpace(source.MusicBrainzRecordingId) ? "no-mbid" : source.MusicBrainzRecordingId);
            keyParts.Add(string.IsNullOrWhiteSpace(source.CodecProfile) ? source.Method.ToString() : source.CodecProfile);
            return string.Join(":", keyParts);
        }

        private static void SetBestSemanticKey(ContentVerificationResult result)
        {
            if (result.SourcesBySemanticKey.Count == 0)
            {
                return;
            }

            var best = result.SourcesBySemanticKey
                .OrderByDescending(kvp => kvp.Value.Count)
                .ThenBy(kvp => kvp.Key.Contains("no-mbid") ? 1 : 0)
                .First();

            result.BestSemanticKey = best.Key;
        }

        /// <inheritdoc/>
        public async Task<string?> GetContentHashAsync(
            string username,
            string filename,
            long fileSize,
            CancellationToken cancellationToken = default)
        {
            if (!TryConsumeProbeBudget(username))
            {
                Telemetry.SwarmMetrics.SwarmVerificationProbesTotal.WithLabels("soulseek", "skipped_budget").Inc();
                Log.Information(
                    "[VERIFY] Skipping probe for {Username}: per-peer-per-day budget exhausted ({Cap})",
                    username,
                    MaxProbesPerPeerPerDay);
                return null;
            }

            var (_, hash, _, _, error) = await VerifySingleSourceAsync(
                username,
                filename,
                fileSize,
                30000,
                cancellationToken);

            return error == null ? hash : null;
        }

        private async Task<(string Username, string? Hash, VerificationMethod Method, long TimeMs, string? Error)> VerifySingleSourceAsync(
            string username,
            string filename,
            long fileSize,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Always use 32KB for byte-level verification (not just FLAC header)
                // Different encodes can have identical FLAC headers but diverge in compressed data
                var isFlac = filename.EndsWith(".flac", StringComparison.OrdinalIgnoreCase);
                var bytesNeeded = VerificationChunkSize;  // Always 32KB for SHA256 verification

                // Don't verify files smaller than our chunk size
                if (fileSize < bytesNeeded)
                {
                    return (username, null, default, stopwatch.ElapsedMilliseconds, "File too small for verification");
                }

                Log.Debug(
                    "Requesting first {Bytes} bytes from {Username} for {Filename} (FLAC: {IsFlac})",
                    bytesNeeded,
                    username,
                    filename,
                    isFlac);

                // Download the verification chunk using a limited stream that cancels after enough bytes
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeoutMs);

                using var memoryStream = new MemoryStream(bytesNeeded);
                using var limitedStream = new LimitedWriteStream(memoryStream, bytesNeeded, cts);

                try
                {
                    await Client.DownloadAsync(
                        username: username,
                        remoteFilename: filename,
                        outputStreamFactory: () => Task.FromResult<Stream>(limitedStream),
                        size: fileSize,  // Pass actual file size to avoid validation error
                        startOffset: 0,
                        cancellationToken: cts.Token,
                        options: new TransferOptions(
                            maximumLingerTime: 1000,
                            disposeOutputStreamOnCompletion: false));
                }
                catch (OperationCanceledException) when (limitedStream.LimitReached)
                {
                    // Expected - we cancelled after getting enough bytes
                    Telemetry.SwarmMetrics.SwarmMidStreamCancellationsTotal.WithLabels("soulseek", "verification_probe").Inc();
                    Log.Debug("Got {Bytes} bytes from {Username}, cancelled remaining transfer", bytesNeeded, username);
                }

                var data = memoryStream.ToArray();

                // ALWAYS use SHA256 of actual bytes for verification
                // FLAC audio MD5 only verifies decoded audio, NOT compressed bytes!
                // Different encodes can have same audio MD5 but different bytes - causes corruption when mixed
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(data);
                var hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
                var method = VerificationMethod.ContentSha256;

                // Log FLAC metadata for debugging if available
                if (isFlac && FlacStreamInfoParser.TryParse(data, out var streamInfo))
                {
                    Log.Debug(
                        "FLAC byte verification for {Username}: SHA256={Hash}, AudioMD5={AudioMd5}, SampleRate={SampleRate}",
                        username,
                        hash.Substring(0, 16) + "...",
                        streamInfo.AudioMd5Hex?.Substring(0, 16) + "...",
                        streamInfo.SampleRate);
                }
                else
                {
                    Log.Debug(
                        "Content verification for {Username}: SHA256={Hash} (first {Bytes} bytes)",
                        username,
                        hash.Substring(0, 16) + "...",
                        data.Length);
                }

                stopwatch.Stop();
                Telemetry.SwarmMetrics.SwarmVerificationProbesTotal.WithLabels("soulseek", "hashed").Inc();
                return (username, hash, method, stopwatch.ElapsedMilliseconds, null);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                Telemetry.SwarmMetrics.SwarmVerificationProbesTotal.WithLabels("soulseek", "failed").Inc();
                Log.Warning("Verification timeout for {Username} on {Filename}", username, filename);
                return (username, null, default, stopwatch.ElapsedMilliseconds, "Timeout");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Telemetry.SwarmMetrics.SwarmVerificationProbesTotal.WithLabels("soulseek", "failed").Inc();
                Log.Warning(ex, "Verification failed for {Username} on {Filename}: {Message}", username, filename, ex.Message);
                return (username, null, default, stopwatch.ElapsedMilliseconds, "Verification failed");
            }
        }

        private async Task<HashDbEntry?> LookupHashDbEntryAsync(string filename, long fileSize, CancellationToken cancellationToken)
        {
            if (HashDb == null)
            {
                return null;
            }

            try
            {
                var flacKey = HashDbEntry.GenerateFlacKey(filename, fileSize);
                return await HashDb.LookupHashAsync(flacKey, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[HASHDB] Unable to lookup entry for {Filename}", filename);
                return null;
            }
        }
    }

    /// <summary>
    ///     A stream wrapper that cancels after writing a specified number of bytes.
    /// </summary>
    public class LimitedWriteStream : Stream
    {
        private readonly Stream innerStream;
        private readonly long limit;
        private readonly CancellationTokenSource cts;
        private long totalBytesWritten;

        public LimitedWriteStream(Stream innerStream, long limit, CancellationTokenSource cts)
        {
            this.innerStream = innerStream;
            this.limit = limit;
            this.cts = cts;
        }

        public bool LimitReached { get; private set; }
        public long BytesWritten => totalBytesWritten;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => innerStream.Length;
        public override long Position { get => innerStream.Position; set => innerStream.Position = value; }

        public override void Flush() => innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (LimitReached)
            {
                return;
            }

            var remaining = limit - totalBytesWritten;
            var toWrite = (int)Math.Min(count, remaining);

            if (toWrite > 0)
            {
                innerStream.Write(buffer, offset, toWrite);
                totalBytesWritten += toWrite;
            }

            if (totalBytesWritten >= limit)
            {
                LimitReached = true;
                cts.Cancel(); // Cancel the download - we have enough
            }
        }
    }
}
