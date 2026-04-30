// <copyright file="LibraryItemsController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.API.Native;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Core.Security;
using slskd.HashDb;
using slskd.Shares;
using slskd.VirtualSoulfind.Core;
using Soulseek;

/// <summary>
/// Provides library items search API for E2E tests and Collections UI.
/// Returns shared files with stable contentId (sha256-based) for deterministic testing.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/library/items")]
[ApiVersion("0")]
[Produces("application/json")]
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class LibraryItemsController : ControllerBase
{
    private readonly IShareService shareService;
    private readonly IHashDbService? hashDbService;
    private readonly ILogger<LibraryItemsController>? logger;
    private readonly IOptionsSnapshot<slskd.Options>? options;

    public LibraryItemsController(
        IShareService shareService,
        IHashDbService? hashDbService = null,
        ILogger<LibraryItemsController>? logger = null,
        IOptionsSnapshot<slskd.Options>? options = null)
    {
        this.shareService = shareService;
        this.hashDbService = hashDbService;
        this.logger = logger;
        this.options = options;
    }

    /// <summary>
    /// Search library items (shared files) by query string.
    /// </summary>
    /// <param name="query">Search query (matches filename).</param>
    /// <param name="kinds">Optional comma-separated list of media kinds (Audio, Video, Book, etc.).</param>
    /// <param name="limit">Maximum number of results (default: 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of library items with stable contentId.</returns>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> SearchItems(
        [FromQuery] string? query = null,
        [FromQuery] string? kinds = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        query = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        kinds = string.IsNullOrWhiteSpace(kinds) ? null : kinds.Trim();
        limit = Math.Clamp(limit, 1, 100);
        logger?.LogInformation("Library items search: query={Query}, kinds={Kinds}, limit={Limit}", query, kinds, limit);

        try
        {
            // Get all shared files
            var allFiles = new List<Soulseek.File>();
            var directories = await shareService.BrowseAsync();

            foreach (var dir in directories)
            {
                if (dir.Files != null)
                {
                    allFiles.AddRange(dir.Files);
                }
            }

            // Filter by query if provided
            IEnumerable<Soulseek.File> filtered = allFiles;
            if (!string.IsNullOrWhiteSpace(query))
            {
                var queryLower = query.ToLowerInvariant();
                filtered = allFiles.Where(f =>
                    f.Filename.ToLowerInvariant().Contains(queryLower));
            }

            // Filter by media kind if provided
            if (!string.IsNullOrWhiteSpace(kinds))
            {
                var kindSet = kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(k => k.ToLowerInvariant())
                    .ToHashSet();

                filtered = filtered.Where(f =>
                {
                    var ext = Path.GetExtension(f.Filename).TrimStart('.').ToLowerInvariant();
                    var kind = GetMediaKind(ext);
                    return kindSet.Contains(kind.ToLowerInvariant());
                });
            }

            // Limit results
            var results = filtered.Take(limit).ToList();

            var codeToMasked = BuildCodeToMaskedFilenameMap();
            var items = new List<LibraryItemResponse>();
            foreach (var file in results)
            {
                var maskedFilename = GetMaskedFilename(file, codeToMasked);
                var item = await ConvertToLibraryItemAsync(file, maskedFilename, cancellationToken);
                if (item != null)
                {
                    items.Add(item);
                }
            }

            if (items.Count == 0 && options != null)
            {
                var fallbackItems = await SearchLocalDirectoriesAsync(
                    query,
                    kinds,
                    limit,
                    cancellationToken);
                return Ok(new { items = fallbackItems });
            }

            return Ok(new { items });
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error searching library items");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Browse library items by virtual share path.
    /// </summary>
    /// <param name="path">Virtual share path to browse. Empty path lists roots.</param>
    /// <param name="query">Optional filename search. When present, searches recursively.</param>
    /// <param name="kinds">Optional comma-separated list of media kinds.</param>
    /// <param name="limit">Maximum number of files to return.</param>
    /// <param name="offset">Zero-based file offset for paging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Folder entries and a paged file result set.</returns>
    [HttpGet("browser")]
    [Authorize]
    public async Task<IActionResult> BrowseItems(
        [FromQuery] string? path = null,
        [FromQuery] string? query = null,
        [FromQuery] string? kinds = "Audio",
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var browserPath = NormalizeVirtualPath(path);
        query = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        kinds = string.IsNullOrWhiteSpace(kinds) ? null : kinds.Trim();
        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(0, offset);

        try
        {
            var directories = (await shareService.BrowseAsync()).ToList();
            var codeToMasked = BuildCodeToMaskedFilenameMap();
            var directoryEntries = query == null
                ? BuildDirectoryEntries(directories, browserPath)
                : new List<LibraryDirectoryResponse>();
            var fileCandidates = BuildFileCandidates(directories, codeToMasked, browserPath, query, kinds);
            var groupedFiles = fileCandidates
                .GroupBy(file => query == null ? file.PathKey : file.DuplicateKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First() with { DuplicateCount = group.Count() })
                .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var pagedFiles = groupedFiles.Skip(offset).Take(limit).ToList();
            var items = new List<LibraryItemResponse>();

            foreach (var file in pagedFiles)
            {
                var item = await ConvertToLibraryItemAsync(
                    file.File,
                    file.RemoteFilename,
                    cancellationToken,
                    file.Path).ConfigureAwait(false);
                if (item != null)
                {
                    item.DuplicateCount = file.DuplicateCount;
                    items.Add(item);
                }
            }

            return Ok(new
            {
                path = browserPath,
                breadcrumbs = BuildBreadcrumbs(browserPath),
                directories = directoryEntries,
                files = items,
                totalFiles = groupedFiles.Count,
                totalDirectories = directoryEntries.Count,
                offset,
                limit,
                hasMore = offset + items.Count < groupedFiles.Count,
                duplicatesRemoved = fileCandidates.Count - groupedFiles.Count,
            });
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error browsing library items");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private async Task<List<LibraryItemResponse>> SearchLocalDirectoriesAsync(
        string? query,
        string? kinds,
        int limit,
        CancellationToken cancellationToken)
    {
        query = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        kinds = string.IsNullOrWhiteSpace(kinds) ? null : kinds.Trim();
        if (options == null)
        {
            return new List<LibraryItemResponse>();
        }

        var localDirs = GetAllowedLocalDirectories();

        if (!localDirs.Any())
        {
            return new List<LibraryItemResponse>();
        }

        var regexOptions = options.Value.Flags.CaseSensitiveRegEx
            ? RegexOptions.None
            : RegexOptions.IgnoreCase;
        var filters = options.Value.Shares.Filters
            .Select(filter => new Regex(filter, regexOptions))
            .ToList();

        var files = localDirs.SelectMany(localDir =>
        {
            try
            {
                return System.IO.Directory.EnumerateFiles(
                    localDir,
                    "*",
                    SearchOption.AllDirectories);
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        });

        if (!string.IsNullOrWhiteSpace(query))
        {
            var queryLower = query.ToLowerInvariant();
            files = files.Where(file =>
                Path.GetFileName(file).ToLowerInvariant().Contains(queryLower));
        }

        if (!string.IsNullOrWhiteSpace(kinds))
        {
            var kindSet = kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(k => k.ToLowerInvariant())
                .ToHashSet();
            files = files.Where(file =>
            {
                var ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
                var kind = GetMediaKind(ext);
                return kindSet.Contains(kind.ToLowerInvariant());
            });
        }

        files = files.Where(file => !filters.Any(filter => filter.IsMatch(file)));

        var items = new List<LibraryItemResponse>();
        foreach (var file in files.Take(limit))
        {
            var item = await ConvertToLibraryItemFromPathAsync(file, localDirs, cancellationToken);
            if (item != null)
            {
                items.Add(item);
            }
        }

        return items;
    }

    private IReadOnlyList<string> GetAllowedLocalDirectories()
    {
        if (options == null)
        {
            return Array.Empty<string>();
        }

        return options.Value.Shares.Directories
            .Select(raw => new Share(raw))
            .Where(share => !share.IsExcluded)
            .Select(share => share.LocalPath)
            .Concat(new[] { options.Value.Directories.Downloads })
            .Where(path => !string.IsNullOrWhiteSpace(path) && System.IO.Directory.Exists(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private async Task<LibraryItemResponse?> ConvertToLibraryItemFromPathAsync(
        string filename,
        IReadOnlyList<string> localDirs,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!System.IO.File.Exists(filename))
            {
                return null;
            }

            var info = new FileInfo(filename);
            var size = info.Length;
            string? sha256 = null;

            if (hashDbService != null)
            {
                try
                {
                    var flacKey = HashDb.Models.HashDbEntry.GenerateFlacKey(filename, size);
                    var hashEntry = await hashDbService.LookupHashAsync(flacKey, cancellationToken);
                    if (hashEntry != null && !string.IsNullOrEmpty(hashEntry.FileSha256))
                    {
                        sha256 = hashEntry.FileSha256;
                    }
                }
                catch
                {
                    // HashDb lookup failed, will compute on-demand if needed
                }
            }

            if (string.IsNullOrEmpty(sha256))
            {
                try
                {
                    sha256 = await ComputeSha256Async(filename, cancellationToken);
                }
                catch
                {
                    // File may not be accessible, skip sha256
                }
            }

            var contentId = !string.IsNullOrEmpty(sha256)
                ? $"sha256:{sha256}"
                : $"path:{slskd.Compute.Sha256Hash($"{filename}|{size}")}";

            try
            {
                shareService.GetLocalRepository().UpsertContentItem(
                    contentId,
                    ContentDomain.GenericFile.ToString(),
                    string.Empty,
                    filename,
                    true,
                    string.Empty,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Failed to upsert local file content item for {Filename}", filename);
            }

            var ext = Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();
            var mediaKind = GetMediaKind(ext);
            var fileName = Path.GetFileName(filename);

            return new LibraryItemResponse
            {
                ContentId = contentId,
                Path = ToDisplayPath(filename, localDirs),
                FileName = fileName,
                Bytes = size,
                MediaKind = mediaKind,
                Sha256 = sha256,
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get library item metadata by contentId.
    /// </summary>
    /// <param name="contentId">Content ID (sha256:... or path-based).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Library item metadata.</returns>
    [HttpGet("{contentId}")]
    [Authorize]
    public async Task<IActionResult> GetItem(
            string contentId,
            CancellationToken cancellationToken = default)
    {
        contentId = contentId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest(new { error = "ContentId is required" });
        }

        logger?.LogInformation("Get library item: contentId={ContentId}", contentId);

        try
        {
            // Search all files to find one matching the contentId
            var directories = await shareService.BrowseAsync();
            Soulseek.File? foundFile = null;

            var codeToMasked = BuildCodeToMaskedFilenameMap();
            foreach (var dir in directories)
            {
                if (dir.Files != null)
                {
                    foreach (var file in dir.Files)
                    {
                        var maskedFilename = GetMaskedFilename(file, codeToMasked);
                        var item = await ConvertToLibraryItemAsync(file, maskedFilename, cancellationToken);
                        if (item != null && item.ContentId == contentId)
                        {
                            foundFile = file;
                            break;
                        }
                    }

                    if (foundFile != null)
                    {
                        break;
                    }
                }
            }

            if (foundFile == null)
            {
                return NotFound(new { error = "Item not found" });
            }

            var foundItem = await ConvertToLibraryItemAsync(
                foundFile,
                GetMaskedFilename(foundFile, codeToMasked),
                cancellationToken);
            return Ok(foundItem);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error getting library item");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private IReadOnlyDictionary<int, string> BuildCodeToMaskedFilenameMap()
    {
        var files = shareService.GetLocalRepository().ListFiles(includeFullPath: true);
        return files
            .GroupBy(file => file.Code)
            .ToDictionary(group => group.Key, group => group.First().Filename);
    }

    private static string GetMaskedFilename(
        Soulseek.File file,
        IReadOnlyDictionary<int, string> codeToMasked)
    {
        if (codeToMasked.TryGetValue(file.Code, out var masked))
        {
            return masked;
        }

        return file.Filename;
    }

    private async Task<LibraryItemResponse?> ConvertToLibraryItemAsync(
        Soulseek.File file,
        string maskedFilename,
        CancellationToken cancellationToken,
        string? displayPath = null)
    {
        try
        {
            // Resolve local file path
            var (host, filename, size) = await shareService.ResolveFileAsync(maskedFilename);

            // Try to get sha256 from HashDb first
            string? sha256 = null;
            if (hashDbService != null)
            {
                try
                {
                    // Try to lookup by size (HashDb uses FlacKey which is based on filename+size)
                    var flacKey = HashDb.Models.HashDbEntry.GenerateFlacKey(filename, size);
                    var hashEntry = await hashDbService.LookupHashAsync(flacKey, cancellationToken);
                    if (hashEntry != null && !string.IsNullOrEmpty(hashEntry.FileSha256))
                    {
                        sha256 = hashEntry.FileSha256;
                    }
                }
                catch
                {
                    // HashDb lookup failed, will compute on-demand if needed
                }
            }

            // If no sha256 from HashDb and file exists, compute it (for test fixtures)
            if (string.IsNullOrEmpty(sha256) && System.IO.File.Exists(filename))
            {
                try
                {
                    sha256 = await ComputeSha256Async(filename, cancellationToken);
                }
                catch
                {
                    // File may not be accessible, skip sha256
                }
            }

            // Generate stable contentId: prefer sha256, fallback to path-based
            var contentId = !string.IsNullOrEmpty(sha256)
                ? $"sha256:{sha256}"
                : $"path:{filename.GetHashCode():X8}:{size}";

            try
            {
                var checkedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var repo = shareService.GetLocalRepository();
                repo.UpsertContentItem(
                    contentId,
                    ContentDomain.GenericFile.ToString(),
                    string.Empty,
                    maskedFilename,
                    true,
                    string.Empty,
                    checkedAt);
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Failed to upsert content item for {Filename}", maskedFilename);
            }

            var ext = Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();
            var mediaKind = GetMediaKind(ext);
            var fileName = GetVirtualFileName(displayPath ?? maskedFilename);

            return new LibraryItemResponse
            {
                ContentId = contentId,
                Path = displayPath ?? maskedFilename,
                FileName = fileName,
                Bytes = size,
                MediaKind = mediaKind,
                Sha256 = sha256,
            };
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to convert file to library item: {Filename}", file.Filename);
            return null;
        }
    }

    private static string GetMediaKind(string extension)
    {
        return extension switch
        {
            "mp3" or "flac" or "ogg" or "opus" or "aac" or "m4a" or "wav" => "Audio",
            "mp4" or "mkv" or "avi" or "mov" or "webm" => "Video",
            "txt" or "pdf" or "epub" or "mobi" => "Book",
            _ => "File",
        };
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);

        var buffer = new byte[32768]; // 32KB chunks
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

        var hashBytes = sha256.Hash ?? throw new InvalidOperationException("SHA256 hash computation failed");
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string ToDisplayPath(string filename, IReadOnlyList<string> roots)
    {
        var fullPath = Path.GetFullPath(filename);
        var root = roots.FirstOrDefault(candidate =>
            fullPath.StartsWith(candidate.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || string.Equals(fullPath, candidate, StringComparison.Ordinal));

        return root == null
            ? Path.GetFileName(filename)
            : Path.GetRelativePath(root, fullPath);
    }

    private static IReadOnlyList<LibraryBreadcrumbResponse> BuildBreadcrumbs(string path)
    {
        var breadcrumbs = new List<LibraryBreadcrumbResponse>
        {
            new() { Name = "Library", Path = string.Empty },
        };
        var current = string.Empty;
        foreach (var part in SplitVirtualPath(path))
        {
            current = string.IsNullOrEmpty(current) ? part : $"{current}\\{part}";
            breadcrumbs.Add(new LibraryBreadcrumbResponse { Name = part, Path = current });
        }

        return breadcrumbs;
    }

    private static List<LibraryDirectoryResponse> BuildDirectoryEntries(
        IReadOnlyList<Soulseek.Directory> directories,
        string path)
    {
        var prefix = string.IsNullOrEmpty(path) ? string.Empty : $"{path}\\";
        return directories
            .Select(directory => NormalizeVirtualPath(directory.Name))
            .Where(directory => !string.Equals(directory, path, StringComparison.OrdinalIgnoreCase))
            .Where(directory => string.IsNullOrEmpty(path)
                ? !directory.Contains('\\')
                : directory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && !directory[prefix.Length..].Contains('\\'))
            .Select(directory =>
            {
                var childPrefix = $"{directory}\\";
                var fileCount = directories
                    .Where(candidate => string.Equals(NormalizeVirtualPath(candidate.Name), directory, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(candidate => candidate.Files ?? Enumerable.Empty<Soulseek.File>())
                    .Count();
                var childDirectoryCount = directories
                    .Select(candidate => NormalizeVirtualPath(candidate.Name))
                    .Count(candidate => candidate.StartsWith(childPrefix, StringComparison.OrdinalIgnoreCase)
                        && !candidate[childPrefix.Length..].Contains('\\'));
                return new LibraryDirectoryResponse
                {
                    Name = SplitVirtualPath(directory).LastOrDefault() ?? directory,
                    Path = directory,
                    FileCount = fileCount,
                    ChildDirectoryCount = childDirectoryCount,
                };
            })
            .OrderBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<LibraryFileCandidate> BuildFileCandidates(
        IReadOnlyList<Soulseek.Directory> directories,
        IReadOnlyDictionary<int, string> codeToMasked,
        string path,
        string? query,
        string? kinds)
    {
        var queryLower = query?.ToLowerInvariant();
        var kindSet = string.IsNullOrWhiteSpace(kinds)
            ? null
            : kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(k => k.ToLowerInvariant())
                .ToHashSet();

        return directories
            .SelectMany(directory =>
            {
                var directoryPath = NormalizeVirtualPath(directory.Name);
                return (directory.Files ?? Enumerable.Empty<Soulseek.File>()).Select(file =>
                {
                    var remoteFilename = codeToMasked.TryGetValue(file.Code, out var masked)
                        ? masked
                        : JoinVirtualPath(directoryPath, file.Filename);
                    var displayPath = NormalizeVirtualPath(remoteFilename);
                    return new LibraryFileCandidate(
                        file,
                        remoteFilename,
                        displayPath,
                        file.Size,
                        GetVirtualFileName(displayPath));
                });
            })
            .Where(candidate => queryLower == null
                ? IsDirectChildFile(candidate.Path, path)
                : candidate.Path.ToLowerInvariant().Contains(queryLower))
            .Where(candidate =>
            {
                if (kindSet == null)
                {
                    return true;
                }

                var ext = Path.GetExtension(candidate.FileName).TrimStart('.').ToLowerInvariant();
                return kindSet.Contains(GetMediaKind(ext).ToLowerInvariant());
            })
            .ToList();
    }

    private static bool IsDirectChildFile(string filePath, string directoryPath)
    {
        filePath = NormalizeVirtualPath(filePath);
        var separatorIndex = filePath.LastIndexOf('\\');
        var parent = separatorIndex < 0 ? string.Empty : filePath[..separatorIndex];
        return string.Equals(parent, directoryPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string JoinVirtualPath(string directory, string filename)
    {
        directory = NormalizeVirtualPath(directory);
        filename = NormalizeVirtualPath(filename);
        return string.IsNullOrEmpty(directory) ? filename : $"{directory}\\{filename}";
    }

    private static string GetVirtualFileName(string path)
    {
        return SplitVirtualPath(path).LastOrDefault() ?? path;
    }

    private static string NormalizeVirtualPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return string.Join(
            "\\",
            path.Replace('/', '\\')
                .Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static IReadOnlyList<string> SplitVirtualPath(string path)
    {
        return NormalizeVirtualPath(path)
            .Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private record LibraryFileCandidate(
        Soulseek.File File,
        string RemoteFilename,
        string Path,
        long Bytes,
        string FileName)
    {
        public int DuplicateCount { get; init; } = 1;
        public string DuplicateKey => $"{FileName}|{Bytes}";
        public string PathKey => Path;
    }

    private class LibraryItemResponse
    {
        public string ContentId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long Bytes { get; set; }
        public string MediaKind { get; set; } = string.Empty;
        public string? Sha256 { get; set; }
        public int DuplicateCount { get; set; } = 1;
    }

    private class LibraryDirectoryResponse
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int FileCount { get; set; }
        public int ChildDirectoryCount { get; set; }
    }

    private class LibraryBreadcrumbResponse
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }
}
