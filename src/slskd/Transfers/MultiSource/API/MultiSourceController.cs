// <copyright file="MultiSourceController.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
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

namespace slskd.Transfers.MultiSource.API
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using Soulseek;
    using IOPath = System.IO.Path;

    /// <summary>
    ///     Experimental multi-source download API.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class MultiSourceController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="MultiSourceController"/> class.
        /// </summary>
        /// <param name="multiSourceService">The multi-source download service.</param>
        /// <param name="soulseekClient">The Soulseek client.</param>
        /// <param name="transferService">The transfer service.</param>
        public MultiSourceController(
            IMultiSourceDownloadService multiSourceService,
            ISoulseekClient soulseekClient,
            ITransferService transferService)
        {
            MultiSource = multiSourceService;
            Client = soulseekClient;
            Transfers = transferService;
        }

        private IMultiSourceDownloadService MultiSource { get; }
        private ISoulseekClient Client { get; }
        private ITransferService Transfers { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<MultiSourceController>();

        // Store last search results for drill-down
        private static List<SearchResponse> LastSearchResults { get; set; } = new();
        private static string LastSearchQuery { get; set; } = string.Empty;

        /// <summary>
        ///     Step 1: Search and get top users ranked by quality.
        /// </summary>
        /// <param name="searchText">The search query.</param>
        /// <returns>Top 10 users with file counts.</returns>
        [HttpGet("users")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> GetTopUsers([FromQuery] string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return BadRequest("Search text is required");
            }

            Log.Information("[MultiSource] Searching for users: {SearchText}", searchText);

            var searchResults = new List<SearchResponse>();

            // Cast a WIDE net: much higher limits for finding multi-source candidates
            var searchOptions = new SearchOptions(
                searchTimeout: 60000,   // 60s after last response (default 15s)
                responseLimit: 5000,    // Up to 5000 peers (default 250)
                fileLimit: 500000,      // Up to 500k files (default 25k)
                filterResponses: true,
                minimumResponseFileCount: 1);

            try
            {
                await Client.SearchAsync(
                    SearchQuery.FromText(searchText),
                    responseHandler: (response) => searchResults.Add(response),
                    options: searchOptions);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[MultiSource] Search failed: {Message}", ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }

            // Store for drill-down
            LastSearchResults = searchResults;
            LastSearchQuery = searchText;

            // Rank users by: free slots, speed, queue length, file count
            var userStats = searchResults
                .GroupBy(r => r.Username)
                .Select(g => new
                {
                    Username = g.Key,
                    FileCount = g.Sum(r => r.Files.Count()),
                    FlacCount = g.Sum(r => r.Files.Count(f => f.Filename.EndsWith(".flac", StringComparison.OrdinalIgnoreCase))),
                    HasFreeSlot = g.Any(r => r.HasFreeUploadSlot),
                    AvgSpeed = g.Average(r => r.UploadSpeed),
                    MinQueue = g.Min(r => r.QueueLength),
                    Score = (g.Any(r => r.HasFreeUploadSlot) ? 1000 : 0) +
                            (g.Average(r => r.UploadSpeed) / 1000) +
                            (100 - Math.Min(g.Min(r => r.QueueLength), 100)) +
                            g.Sum(r => r.Files.Count(f => f.Filename.EndsWith(".flac", StringComparison.OrdinalIgnoreCase))),
                })
                .OrderByDescending(u => u.Score)
                .Take(10)
                .Select((u, i) => new
                {
                    Index = i + 1,
                    u.Username,
                    u.FileCount,
                    u.FlacCount,
                    u.HasFreeSlot,
                    Speed = $"{u.AvgSpeed / 1024:F0} KB/s",
                    Queue = u.MinQueue,
                    u.Score,
                })
                .ToList();

            return Ok(new
            {
                query = searchText,
                totalResponses = searchResults.Count,
                users = userStats,
                hint = "Use /api/v0/multisource/users/{username}/files to list files from a user",
            });
        }

        /// <summary>
        ///     Step 2: List files from a specific user (from last search).
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="filter">Optional filter (e.g., "flac").</param>
        /// <returns>Files from the user.</returns>
        [HttpGet("users/{username}/files")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetUserFiles(string username, [FromQuery] string filter = null)
        {
            if (LastSearchResults == null || LastSearchResults.Count == 0)
            {
                return BadRequest("No search results. Call /users?searchText=... first");
            }

            var userResponses = LastSearchResults.Where(r => r.Username.Equals(username, StringComparison.OrdinalIgnoreCase)).ToList();

            if (userResponses.Count == 0)
            {
                return NotFound($"User '{username}' not found in last search results");
            }

            var files = userResponses
                .SelectMany(r => r.Files)
                .Where(f => string.IsNullOrEmpty(filter) || f.Filename.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .Select((f, i) => new
                {
                    Index = i + 1,
                    Filename = IOPath.GetFileName(f.Filename),
                    FullPath = f.Filename,
                    f.Size,
                    SizeMB = $"{f.Size / 1024.0 / 1024.0:F1} MB",
                    f.BitRate,
                    f.SampleRate,
                    f.BitDepth,
                    Extension = IOPath.GetExtension(f.Filename).ToLowerInvariant(),
                })
                .OrderBy(f => f.FullPath)
                .ToList();

                // Group by directory for easier browsing
                var directories = files
                    .GroupBy(f => IOPath.GetDirectoryName(f.FullPath)?.Replace("\\", "/") ?? string.Empty)
                    .Select(g => new
                    {
                        Directory = g.Key.Split('/').LastOrDefault() ?? g.Key,
                        FullDirectory = g.Key,
                        FileCount = g.Count(),
                        Files = g.Take(20).ToList(), // Limit files shown per dir
                    })
                    .OrderBy(d => d.Directory)
                    .ToList();

            return Ok(new
            {
                username,
                lastQuery = LastSearchQuery,
                filter,
                totalFiles = files.Count,
                directories,
                hint = "Pick a file and use POST /api/v0/multisource/file-sources with {filename, size} to find other sources",
            });
        }

        /// <summary>
        ///     Step 3: Find all sources for a specific file.
        /// </summary>
        /// <param name="request">The file to search for.</param>
        /// <returns>All sources that have this exact file.</returns>
        [HttpPost("file-sources")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> FindFileSources([FromBody] FileSourceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Filename))
            {
                return BadRequest("Filename is required");
            }

            var searchTerm = IOPath.GetFileNameWithoutExtension(request.Filename);
            Log.Information("[MultiSource] Searching for file sources: {Filename} ({Size} bytes)", request.Filename, request.Size);

            var searchResults = new List<SearchResponse>();

            // Use wider search for file-sources to find all matches
            var searchOptions = new SearchOptions(
                searchTimeout: 30000,   // 30s timeout for better coverage
                responseLimit: 1000,    // Allow more responses
                fileLimit: 100000,
                filterResponses: true,
                minimumResponseFileCount: 1);

            try
            {
                await Client.SearchAsync(
                    SearchQuery.FromText(searchTerm),
                    responseHandler: (response) => searchResults.Add(response),
                    options: searchOptions);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[MultiSource] Search failed: {Message}", ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }

            // Find exact matches
            var targetFilename = IOPath.GetFileName(request.Filename);
            var sources = new List<object>();

            foreach (var response in searchResults)
            {
                foreach (var file in response.Files)
                {
                    var filename = IOPath.GetFileName(file.Filename);
                    var sizeMatch = request.Size == 0 || file.Size == request.Size;
                    var nameMatch = filename.Equals(targetFilename, StringComparison.OrdinalIgnoreCase);

                    if (nameMatch && sizeMatch)
                    {
                        sources.Add(new
                        {
                            response.Username,
                            FullPath = file.Filename,
                            file.Size,
                            SizeMB = $"{file.Size / 1024.0 / 1024.0:F1} MB",
                            response.HasFreeUploadSlot,
                            QueueLength = (int)response.QueueLength,
                            Speed = $"{response.UploadSpeed / 1024:F0} KB/s",
                            file.BitRate,
                            file.SampleRate,
                        });
                    }
                }
            }

            // Group by size to show which files are identical
            var bySize = sources
                .GroupBy(s => ((dynamic)s).Size)
                .Select(g => new
                {
                    Size = (long)g.Key,
                    SizeMB = $"{(long)g.Key / 1024.0 / 1024.0:F1} MB",
                    SourceCount = g.Count(),
                    Sources = g.ToList(),
                })
                .OrderByDescending(g => g.SourceCount)
                .ToList();

            var bestGroup = bySize.FirstOrDefault();

            return Ok(new
            {
                filename = targetFilename,
                requestedSize = request.Size,
                totalSources = sources.Count,
                sizeGroups = bySize,
                bestMatch = bestGroup != null ? new
                {
                    bestGroup.Size,
                    bestGroup.SizeMB,
                    bestGroup.SourceCount,
                    canMultiSource = bestGroup.SourceCount >= 2,
                } : null,
                hint = bestGroup?.SourceCount >= 2
                    ? $"Use POST /api/v0/multisource/download-file with filename and size={bestGroup.Size} to start multi-source download"
                    : "Not enough sources with identical file size for multi-source download",
            });
        }

        /// <summary>
        ///     Step 4: Download a file from multiple sources.
        /// </summary>
        /// <param name="request">The download request.</param>
        /// <returns>Download result.</returns>
        [HttpPost("download-file")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> DownloadFile([FromBody] FileSourceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Filename) || request.Size == 0)
            {
                return BadRequest("Filename and size are required");
            }

            // First find sources with wide search
            var searchTerm = IOPath.GetFileNameWithoutExtension(request.Filename);
            var searchResults = new List<SearchResponse>();

            try
            {
                await Client.SearchAsync(
                    SearchQuery.FromText(searchTerm),
                    responseHandler: (response) => searchResults.Add(response),
                    options: new SearchOptions(
                        searchTimeout: 30000,
                        responseLimit: 1000,
                        fileLimit: 100000,
                        filterResponses: true,
                        minimumResponseFileCount: 1));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }

            // Find exact matches with same size
            var targetFilename = IOPath.GetFileName(request.Filename);
            var sources = new List<(string Username, string FullPath)>();

            foreach (var response in searchResults)
            {
                foreach (var file in response.Files)
                {
                    var filename = IOPath.GetFileName(file.Filename);
                    if (filename.Equals(targetFilename, StringComparison.OrdinalIgnoreCase) && file.Size == request.Size)
                    {
                        sources.Add((response.Username, file.Filename));
                    }
                }
            }

            if (sources.Count < 2)
            {
                return BadRequest($"Not enough sources ({sources.Count}). Need at least 2 for multi-source download.");
            }

            // Verify sources
            Log.Information("[MultiSource] Verifying {Count} sources for {Filename}", sources.Count, targetFilename);

            var verificationResult = await MultiSource.FindVerifiedSourcesAsync(
                sources.First().FullPath,
                request.Size,
                cancellationToken: HttpContext.RequestAborted);

            if (verificationResult.BestSources.Count < 2)
            {
                return BadRequest($"Not enough verified sources ({verificationResult.BestSources.Count}). Verification may have failed.");
            }

            // Download
            var outputPath = IOPath.Combine(IOPath.GetTempPath(), "slskdn-test", $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{targetFilename}");

            var downloadResult = await MultiSource.DownloadAsync(
                new MultiSourceDownloadRequest
                {
                    Filename = sources.First().FullPath,
                    FileSize = request.Size,
                    ExpectedHash = verificationResult.BestHash,
                    OutputPath = outputPath,
                    Sources = verificationResult.BestSources,
                },
                HttpContext.RequestAborted);

            return Ok(downloadResult);
        }

        /// <summary>
        ///     SWARM/RACE MODE: Queue ALL sources to download full file.
        ///     First to complete wins. No chunking (Soulseek doesn't support it well).
        /// </summary>
        /// <param name="request">The swarm download request.</param>
        /// <returns>Download result with per-source stats.</returns>
        [HttpPost("swarm")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> SwarmDownload([FromBody] SwarmDownloadRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Filename) || request.Size == 0)
            {
                return BadRequest("Filename and size are required. Use search term for filename, exact size in bytes.");
            }

            Log.Information("[SWARM] Starting RACE MODE: {Filename} ({Size} bytes)", request.Filename, request.Size);

            // Search for ALL sources
            var searchTerm = request.Filename;
            var searchResults = new List<SearchResponse>();

            try
            {
                await Client.SearchAsync(
                    SearchQuery.FromText(searchTerm),
                    responseHandler: (response) => searchResults.Add(response),
                    options: new SearchOptions(
                        searchTimeout: request.SearchTimeout,
                        responseLimit: 2000,
                        fileLimit: 100000,
                        filterResponses: true,
                        minimumResponseFileCount: 1));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Search failed: {ex.Message}" });
            }

            Log.Information("[SWARM] Search complete: {Count} responses", searchResults.Count);

            // Find ALL sources with MATCHING SIZE
            var allSources = new List<(string Username, string FullPath, int Speed)>();

            foreach (var response in searchResults)
            {
                foreach (var file in response.Files)
                {
                    if (file.Size == request.Size)
                    {
                        if (!allSources.Any(s => s.Username.Equals(response.Username, StringComparison.OrdinalIgnoreCase)))
                        {
                            allSources.Add((response.Username, file.Filename, response.UploadSpeed));
                        }
                    }
                }
            }

            // Sort by speed descending
            allSources = allSources.OrderByDescending(s => s.Speed).ToList();

            Log.Information("[SWARM] Found {Count} sources with size {Size}", allSources.Count, request.Size);

            if (allSources.Count < 2)
            {
                return BadRequest($"Not enough sources ({allSources.Count}). Need at least 2.");
            }

            Log.Information("[SWARM] Starting REAL swarm with {Count} sources, {ChunkSize} byte chunks", allSources.Count, request.ChunkSize);

            // Convert to VerifiedSource list for the swarm service
            var verifiedSources = allSources.Select(s => new VerifiedSource
            {
                Username = s.Username,
                FullPath = s.FullPath,
                Method = VerificationMethod.None,
            }).ToList();

            // Calculate chunks for display
            var numChunks = (int)Math.Ceiling((double)request.Size / request.ChunkSize);

            // Call the REAL swarm download service
            var targetFilename = IOPath.GetFileName(verifiedSources.First().FullPath);
            var outputPath = IOPath.Combine(IOPath.GetTempPath(), "slskdn-swarm", $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{targetFilename}");

            var downloadResult = await MultiSource.DownloadAsync(
                new MultiSourceDownloadRequest
                {
                    Filename = verifiedSources.First().FullPath,
                    FileSize = request.Size,
                    OutputPath = outputPath,
                    Sources = verifiedSources,
                    ChunkSize = request.ChunkSize,
                },
                HttpContext.RequestAborted);

            return Ok(new
            {
                mode = "SWARM",
                description = "All sources grabbing chunks from shared queue. Fast users do more!",
                success = downloadResult.Success,
                fileSize = request.Size,
                fileSizeMB = $"{request.Size / 1024.0 / 1024.0:F1} MB",
                chunkSize = request.ChunkSize,
                chunkSizeKB = request.ChunkSize / 1024,
                totalChunks = numChunks,
                totalSources = allSources.Count,
                sourcesUsed = downloadResult.SourcesUsed,
                timeMs = downloadResult.TotalTimeMs,
                timeSeconds = downloadResult.TotalTimeMs > 0 ? $"{downloadResult.TotalTimeMs / 1000.0:F1}s" : "N/A",
                speedMBps = downloadResult.TotalTimeMs > 0
                    ? $"{(request.Size / 1024.0 / 1024.0) / (downloadResult.TotalTimeMs / 1000.0):F2} MB/s"
                    : "N/A",
                outputPath = downloadResult.OutputPath,
                finalHash = downloadResult.FinalHash,
                error = downloadResult.Error,
                chunks = downloadResult.Chunks?.GroupBy(c => c.Username)
                    .Select(g => new { User = g.Key, ChunksCompleted = g.Count(c => c.Success), ChunksFailed = g.Count(c => !c.Success) })
                    .OrderByDescending(g => g.ChunksCompleted),
            });
        }

        /// <summary>
        ///     Searches for files and returns candidates for multi-source download.
        /// </summary>
        /// <param name="searchText">The search query.</param>
        /// <returns>Search results with verification info.</returns>
        [HttpGet("search")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> Search([FromQuery] string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return BadRequest("Search text is required");
            }

            Log.Information("[MultiSource] Searching for: {SearchText}", searchText);

            var searchResults = new List<SearchResponse>();
            var searchOptions = new SearchOptions(
                filterResponses: true,
                minimumResponseFileCount: 1,
                responseLimit: 100);

            try
            {
                await Client.SearchAsync(
                    SearchQuery.FromText(searchText),
                    responseHandler: (response) => searchResults.Add(response),
                    options: searchOptions);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[MultiSource] Search failed: {Message}", ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }

            // Group files by filename + size (potential multi-source candidates)
            var fileGroups = new Dictionary<string, MultiSourceCandidate>();

            foreach (var response in searchResults)
            {
                foreach (var file in response.Files)
                {
                    var filename = IOPath.GetFileName(file.Filename);
                    var key = $"{filename}|{file.Size}";

                    if (!fileGroups.TryGetValue(key, out var candidate))
                    {
                        candidate = new MultiSourceCandidate
                        {
                            Filename = filename,
                            Size = file.Size,
                            Extension = IOPath.GetExtension(filename).ToLowerInvariant(),
                            Sources = new List<SourceInfo>(),
                        };
                        fileGroups[key] = candidate;
                    }

                    // Store full path for first occurrence
                    if (string.IsNullOrEmpty(candidate.FullPath))
                    {
                        candidate.FullPath = file.Filename;
                    }

                    candidate.Sources.Add(new SourceInfo
                    {
                        Username = response.Username,
                        FullPath = file.Filename,
                        HasFreeUploadSlot = response.HasFreeUploadSlot,
                        QueueLength = (int)response.QueueLength,
                        UploadSpeed = response.UploadSpeed,
                        BitRate = file.BitRate,
                        SampleRate = file.SampleRate,
                        BitDepth = file.BitDepth,
                    });
                }
            }

            // Sort by source count (most sources first) and return top candidates
            var candidates = fileGroups.Values
                .Where(c => c.Sources.Count >= 2) // Only files with multiple sources
                .OrderByDescending(c => c.Sources.Count)
                .ThenByDescending(c => c.Size)
                .Take(50)
                .ToList();

            Log.Information(
                "[MultiSource] Found {Total} files, {Candidates} with multiple sources",
                fileGroups.Count,
                candidates.Count);

            return Ok(new
            {
                query = searchText,
                totalFiles = fileGroups.Count,
                multiSourceCandidates = candidates.Count,
                candidates,
            });
        }

        /// <summary>
        ///     Verifies sources for a specific file.
        /// </summary>
        /// <param name="request">The verification request.</param>
        /// <returns>Verification results with sources grouped by content hash.</returns>
        [HttpPost("verify")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> VerifySources([FromBody] VerifyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Filename))
            {
                return BadRequest("Filename is required");
            }

            if (request.Usernames == null || request.Usernames.Count == 0)
            {
                return BadRequest("At least one username is required");
            }

            Log.Information(
                "[MultiSource] Verifying {Count} sources for {Filename}",
                request.Usernames.Count,
                request.Filename);

            var result = await MultiSource.FindVerifiedSourcesAsync(
                request.Filename,
                request.FileSize,
                cancellationToken: HttpContext.RequestAborted);

            return Ok(result);
        }

        /// <summary>
        ///     Starts a multi-source download.
        /// </summary>
        /// <param name="request">The download request.</param>
        /// <returns>Download result.</returns>
        [HttpPost("download")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> Download([FromBody] DownloadRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Filename))
            {
                return BadRequest("Filename is required");
            }

            if (request.Sources == null || request.Sources.Count < 2)
            {
                return BadRequest("At least 2 verified sources are required");
            }

            var outputPath = IOPath.Combine(
                IOPath.GetTempPath(),
                "slskdn-test",
                IOPath.GetFileName(request.Filename));

            Log.Information(
                "[MultiSource] Starting download of {Filename} from {Count} sources",
                request.Filename,
                request.Sources.Count);

            var downloadRequest = new MultiSourceDownloadRequest
            {
                Filename = request.Filename,
                FileSize = request.FileSize,
                ExpectedHash = request.ExpectedHash,
                OutputPath = outputPath,
                ChunkSize = request.ChunkSize,
                Sources = request.Sources.Select(s => new VerifiedSource
                {
                    Username = s.Username,
                    FullPath = s.FullPath ?? request.Filename,
                    ContentHash = s.ContentHash,
                    Method = s.Method,
                }).ToList(),
            };

            Log.Information("[SWARM] Starting with {Sources} sources, {ChunkSize}KB chunks",
                request.Sources.Count, request.ChunkSize / 1024);

            var result = await MultiSource.DownloadAsync(downloadRequest, HttpContext.RequestAborted);

            return Ok(result);
        }

        /// <summary>
        ///     One-click test: search, verify, and download.
        /// </summary>
        /// <param name="request">The test request.</param>
        /// <returns>Complete test results.</returns>
        [HttpPost("test")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> RunTest([FromBody] TestRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.SearchText))
            {
                return BadRequest("Search text is required");
            }

            var testResult = new TestResult
            {
                SearchText = request.SearchText,
                StartedAt = DateTime.UtcNow,
            };

            Log.Information("[MultiSource] Starting test for: {SearchText}", request.SearchText);

            // Step 1: Wide net search
            var searchResults = new List<SearchResponse>();
            try
            {
                await Client.SearchAsync(
                    SearchQuery.FromText(request.SearchText),
                    responseHandler: (r) => searchResults.Add(r),
                    options: new SearchOptions(
                        searchTimeout: 45000,   // 45s for wide coverage
                        responseLimit: 2000,    // Up to 2000 peers
                        fileLimit: 100000,
                        filterResponses: true,
                        minimumResponseFileCount: 1));

                testResult.SearchResponseCount = searchResults.Count;
            }
            catch (Exception ex)
            {
                testResult.Error = $"Search failed: {ex.Message}";
                return Ok(testResult);
            }

            // Step 2: Find best candidate (most sources, FLAC preferred)
            var candidates = new Dictionary<string, (string Filename, long Size, List<string> Users)>();

            foreach (var response in searchResults)
            {
                foreach (var file in response.Files)
                {
                    var fname = IOPath.GetFileName(file.Filename);
                    var key = $"{fname}|{file.Size}";

                    if (!candidates.TryGetValue(key, out var c))
                    {
                        c = (file.Filename, file.Size, new List<string>());
                        candidates[key] = c;
                    }

                    if (!c.Users.Contains(response.Username))
                    {
                        c.Users.Add(response.Username);
                    }
                }
            }

            var bestCandidate = candidates.Values
                .Where(c => c.Users.Count >= 2)
                .OrderByDescending(c => c.Filename.EndsWith(".flac", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenByDescending(c => c.Users.Count)
                .FirstOrDefault();

            if (bestCandidate.Filename == null)
            {
                testResult.Error = "No files with multiple sources found";
                return Ok(testResult);
            }

            testResult.SelectedFile = IOPath.GetFileName(bestCandidate.Filename);
            testResult.FileSize = bestCandidate.Size;
            testResult.CandidateSources = bestCandidate.Users.Count;

            Log.Information(
                "[MultiSource] Best candidate: {File} ({Size} bytes) with {Sources} sources",
                testResult.SelectedFile,
                testResult.FileSize,
                testResult.CandidateSources);

            // Step 3: Verify sources
            var verificationResult = await MultiSource.FindVerifiedSourcesAsync(
                bestCandidate.Filename,
                bestCandidate.Size,
                cancellationToken: HttpContext.RequestAborted);

            testResult.VerifiedSources = verificationResult.BestSources.Count;
            testResult.VerificationMethod = verificationResult.BestSources.FirstOrDefault()?.Method.ToString();
            testResult.ContentHash = verificationResult.BestHash;

            if (verificationResult.BestSources.Count < 2)
            {
                testResult.Error = $"Not enough verified sources (got {verificationResult.BestSources.Count})";
                return Ok(testResult);
            }

            // Step 4: Download
            var outputPath = IOPath.Combine(
                IOPath.GetTempPath(),
                "slskdn-test",
                $"multitest_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{IOPath.GetFileName(bestCandidate.Filename)}");

            var downloadRequest = new MultiSourceDownloadRequest
            {
                Filename = bestCandidate.Filename,
                FileSize = bestCandidate.Size,
                ExpectedHash = verificationResult.BestHash,
                OutputPath = outputPath,
                Sources = verificationResult.BestSources,
            };

            var downloadResult = await MultiSource.DownloadAsync(downloadRequest, HttpContext.RequestAborted);

            testResult.DownloadSuccess = downloadResult.Success;
            testResult.DownloadTimeMs = downloadResult.TotalTimeMs;
            testResult.BytesDownloaded = downloadResult.BytesDownloaded;
            testResult.SourcesUsed = downloadResult.SourcesUsed;
            testResult.OutputPath = downloadResult.OutputPath;
            testResult.FinalHash = downloadResult.FinalHash;
            testResult.CompletedAt = DateTime.UtcNow;

            if (downloadResult.TotalTimeMs > 0)
            {
                testResult.AverageSpeedMBps = (downloadResult.BytesDownloaded / 1024.0 / 1024.0) / (downloadResult.TotalTimeMs / 1000.0);
            }

            if (!downloadResult.Success)
            {
                testResult.Error = downloadResult.Error;
            }

            Log.Information(
                "[MultiSource] Test complete: {Success}, {Size} bytes in {Time}ms ({Speed:F2} MB/s)",
                testResult.DownloadSuccess,
                testResult.BytesDownloaded,
                testResult.DownloadTimeMs,
                testResult.AverageSpeedMBps);

            return Ok(testResult);
        }
    }

    /// <summary>
    ///     A candidate file for multi-source download.
    /// </summary>
    public class MultiSourceCandidate
    {
        /// <summary>Gets or sets the filename.</summary>
        public string Filename { get; set; }

        /// <summary>Gets or sets the full path from first source.</summary>
        public string FullPath { get; set; }

        /// <summary>Gets or sets the file size.</summary>
        public long Size { get; set; }

        /// <summary>Gets or sets the file extension.</summary>
        public string Extension { get; set; }

        /// <summary>Gets or sets the list of sources.</summary>
        public List<SourceInfo> Sources { get; set; }

        /// <summary>Gets the source count.</summary>
        public int SourceCount => Sources?.Count ?? 0;
    }

    /// <summary>
    ///     Information about a source.
    /// </summary>
    public class SourceInfo
    {
        /// <summary>Gets or sets the username.</summary>
        public string Username { get; set; }

        /// <summary>Gets or sets the full path.</summary>
        public string FullPath { get; set; }

        /// <summary>Gets or sets whether user has free upload slots.</summary>
        public bool HasFreeUploadSlot { get; set; }

        /// <summary>Gets or sets queue length.</summary>
        public int QueueLength { get; set; }

        /// <summary>Gets or sets upload speed.</summary>
        public int UploadSpeed { get; set; }

        /// <summary>Gets or sets bit rate.</summary>
        public int? BitRate { get; set; }

        /// <summary>Gets or sets sample rate.</summary>
        public int? SampleRate { get; set; }

        /// <summary>Gets or sets bit depth.</summary>
        public int? BitDepth { get; set; }
    }

    /// <summary>
    ///     Request to verify sources.
    /// </summary>
    public class VerifyRequest
    {
        /// <summary>Gets or sets the filename.</summary>
        public string Filename { get; set; }

        /// <summary>Gets or sets the file size.</summary>
        public long FileSize { get; set; }

        /// <summary>Gets or sets the usernames to verify.</summary>
        public List<string> Usernames { get; set; }
    }

    /// <summary>
    ///     Request to download from multiple sources.
    /// </summary>
    public class DownloadRequest
    {
        /// <summary>Gets or sets the filename.</summary>
        public string Filename { get; set; }

        /// <summary>Gets or sets the file size.</summary>
        public long FileSize { get; set; }

        /// <summary>Gets or sets the expected content hash.</summary>
        public string ExpectedHash { get; set; }

        /// <summary>Gets or sets the chunk size in bytes (default 256KB).</summary>
        public long ChunkSize { get; set; } = 128 * 1024;

        /// <summary>Gets or sets the verified sources.</summary>
        public List<SourceRequest> Sources { get; set; }
    }

    /// <summary>
    ///     A source in a download request.
    /// </summary>
    public class SourceRequest
    {
        /// <summary>Gets or sets the username.</summary>
        public string Username { get; set; }

        /// <summary>Gets or sets the full path on the user's share.</summary>
        public string FullPath { get; set; }

        /// <summary>Gets or sets the content hash.</summary>
        public string ContentHash { get; set; }

        /// <summary>Gets or sets the verification method.</summary>
        public VerificationMethod Method { get; set; }
    }

    /// <summary>
    ///     Request for a one-click test.
    /// </summary>
    public class TestRequest
    {
        /// <summary>Gets or sets the search text.</summary>
        public string SearchText { get; set; }
    }

    /// <summary>
    ///     Result of a multi-source test.
    /// </summary>
    public class TestResult
    {
        /// <summary>Gets or sets the search text.</summary>
        public string SearchText { get; set; }

        /// <summary>Gets or sets when the test started.</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>Gets or sets when the test completed.</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>Gets or sets the search response count.</summary>
        public int SearchResponseCount { get; set; }

        /// <summary>Gets or sets the selected file.</summary>
        public string SelectedFile { get; set; }

        /// <summary>Gets or sets the file size.</summary>
        public long FileSize { get; set; }

        /// <summary>Gets or sets the number of candidate sources.</summary>
        public int CandidateSources { get; set; }

        /// <summary>Gets or sets the number of verified sources.</summary>
        public int VerifiedSources { get; set; }

        /// <summary>Gets or sets the verification method used.</summary>
        public string VerificationMethod { get; set; }

        /// <summary>Gets or sets the content hash.</summary>
        public string ContentHash { get; set; }

        /// <summary>Gets or sets whether download succeeded.</summary>
        public bool DownloadSuccess { get; set; }

        /// <summary>Gets or sets the download time in ms.</summary>
        public long DownloadTimeMs { get; set; }

        /// <summary>Gets or sets bytes downloaded.</summary>
        public long BytesDownloaded { get; set; }

        /// <summary>Gets or sets sources used.</summary>
        public int SourcesUsed { get; set; }

        /// <summary>Gets or sets the output path.</summary>
        public string OutputPath { get; set; }

        /// <summary>Gets or sets the final hash.</summary>
        public string FinalHash { get; set; }

        /// <summary>Gets or sets average speed in MB/s.</summary>
        public double AverageSpeedMBps { get; set; }

        /// <summary>Gets or sets the error message.</summary>
        public string Error { get; set; }
    }

    /// <summary>
    ///     Request to find sources for a specific file.
    /// </summary>
    public class FileSourceRequest
    {
        /// <summary>Gets or sets the filename.</summary>
        public string Filename { get; set; }

        /// <summary>Gets or sets the file size (0 to match any size).</summary>
        public long Size { get; set; }
    }

    /// <summary>
    ///     Request for swarm download.
    /// </summary>
    public class SwarmDownloadRequest
    {
        /// <summary>Gets or sets the filename to search for.</summary>
        public string Filename { get; set; }

        /// <summary>Gets or sets the exact file size.</summary>
        public long Size { get; set; }

        /// <summary>Gets or sets the chunk size (default 256KB).</summary>
        public int ChunkSize { get; set; } = 128 * 1024;

        /// <summary>Gets or sets the search timeout in ms (default 30s).</summary>
        public int SearchTimeout { get; set; } = 30000;

        /// <summary>Gets or sets whether to skip verification (faster).</summary>
        public bool SkipVerification { get; set; } = true;
    }
}
