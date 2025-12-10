namespace slskd.Transfers.MultiSource.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;

    public interface ISwarmEventStore
    {
        Task AppendAsync(SwarmEvent evt, CancellationToken ct = default);

        Task<IReadOnlyList<SwarmEvent>> ReadAsync(string jobId, int limit = 2000, CancellationToken ct = default);
    }

    /// <summary>
    ///     File-based event store with simple rotation and retention.
    /// </summary>
    public class SwarmEventStore : ISwarmEventStore
    {
        private const int MaxJobs = 200;
        private const long MaxPerJobBytes = 5 * 1024 * 1024; // 5 MB
        private static readonly TimeSpan Ttl = TimeSpan.FromDays(7);

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly string sessionsDir;
        private readonly ILogger log = Log.ForContext<SwarmEventStore>();

        public SwarmEventStore()
        {
            sessionsDir = Path.Combine(Program.AppDirectory ?? ".", "logs", "sessions");
            Directory.CreateDirectory(sessionsDir);
        }

        public async Task AppendAsync(SwarmEvent evt, CancellationToken ct = default)
        {
            if (evt == null || string.IsNullOrWhiteSpace(evt.JobId))
            {
                return;
            }

            ct.ThrowIfCancellationRequested();

            var path = GetPath(evt.JobId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var json = JsonSerializer.Serialize(evt, JsonOpts) + Environment.NewLine;
            await File.AppendAllTextAsync(path, json, Encoding.UTF8, ct).ConfigureAwait(false);

            RotateIfNeeded(path);
            CleanupOldFiles();
        }

        public async Task<IReadOnlyList<SwarmEvent>> ReadAsync(string jobId, int limit = 2000, CancellationToken ct = default)
        {
            var list = new List<SwarmEvent>();
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return list;
            }

            var path = GetPath(jobId);
            if (!File.Exists(path))
            {
                return list;
            }

            await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var evt = JsonSerializer.Deserialize<SwarmEvent>(line, JsonOpts);
                    if (evt != null)
                    {
                        list.Add(evt);
                        if (list.Count >= limit)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Debug(ex, "[SwarmEventStore] Failed to deserialize event line");
                }
            }

            return list;
        }

        private string GetPath(string jobId) => Path.Combine(sessionsDir, $"{jobId}.log");

        private void RotateIfNeeded(string path)
        {
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists)
                {
                    return;
                }

                if (fi.Length <= MaxPerJobBytes)
                {
                    return;
                }

                var archived = Path.ChangeExtension(path, ".log.old");
                if (File.Exists(archived))
                {
                    File.Delete(archived);
                }

                File.Move(path, archived);
            }
            catch (Exception ex)
            {
                log.Debug(ex, "[SwarmEventStore] Rotation failed for {Path}", path);
            }
        }

        private void CleanupOldFiles()
        {
            try
            {
                var files = Directory.GetFiles(sessionsDir, "*.log", SearchOption.TopDirectoryOnly)
                    .Select(f => new FileInfo(f))
                    .Where(f => f.Exists)
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .ToList();

                var now = DateTime.UtcNow;

                foreach (var file in files.ToList())
                {
                    if (now - file.LastWriteTimeUtc > Ttl)
                    {
                        TryDelete(file.FullName);
                    }
                }

                if (files.Count > MaxJobs)
                {
                    foreach (var file in files.Skip(MaxJobs))
                    {
                        TryDelete(file.FullName);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Debug(ex, "[SwarmEventStore] Cleanup failed");
            }
        }

        private void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                log.Debug(ex, "[SwarmEventStore] Failed to delete {Path}", path);
            }
        }
    }
}

