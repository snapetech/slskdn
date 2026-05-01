// <copyright file="ContentLocator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Streaming;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Shares;

/// <summary>
/// Resolves contentId via the local IShareRepository (FindContentItem → FindFileInfo). Path comes from originalFilename (local path).
/// Never uses client-supplied paths. Enforces IsAdvertisable.
/// </summary>
public sealed class ContentLocator : IContentLocator
{
    private const int MaxFallbackFilesToScan = 5000;

    private readonly IShareService _shareService;
    private readonly ILogger<ContentLocator> _log;
    private readonly IOptionsMonitor<slskd.Options>? _options;

    public ContentLocator(
        IShareService shareService,
        ILogger<ContentLocator> log,
        IOptionsMonitor<slskd.Options>? options = null)
    {
        _shareService = shareService ?? throw new ArgumentNullException(nameof(shareService));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _options = options;
    }

    /// <inheritdoc />
    public ResolvedContent? Resolve(string contentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
            return null;

        var repo = _shareService.GetLocalRepository();
        var ci = repo.FindContentItem(contentId);
        if (ci == null)
        {
            _log.LogDebug("[ContentLocator] ContentId {ContentId} not found", contentId);
            return ResolveFromAllowedLocalRoots(contentId, cancellationToken);
        }

        if (!ci.Value.IsAdvertisable)
        {
            _log.LogDebug("[ContentLocator] ContentId {ContentId} is not advertisable", contentId);
            return null;
        }

        var finfo = repo.FindFileInfo(ci.Value.MaskedFilename);
        if (string.IsNullOrEmpty(finfo.Filename) &&
            Path.IsPathFullyQualified(ci.Value.MaskedFilename) &&
            IsAllowedLocalPath(ci.Value.MaskedFilename) &&
            File.Exists(ci.Value.MaskedFilename))
        {
            var info = new FileInfo(ci.Value.MaskedFilename);
            finfo = (ci.Value.MaskedFilename, info.Length);
        }

        if (string.IsNullOrEmpty(finfo.Filename) || finfo.Size <= 0)
        {
            _log.LogDebug("[ContentLocator] FindFileInfo returned no path for {Masked}", ci.Value.MaskedFilename);
            return null;
        }

        if (!File.Exists(finfo.Filename))
        {
            _log.LogDebug("[ContentLocator] File no longer on disk: {Path}", finfo.Filename);
            return null;
        }

        var contentType = GetContentType(finfo.Filename);
        return new ResolvedContent(finfo.Filename, finfo.Size, contentType);
    }

    private ResolvedContent? ResolveFromAllowedLocalRoots(string contentId, CancellationToken cancellationToken)
    {
        if (_options == null)
        {
            return null;
        }

        var roots = GetAllowedLocalRoots();
        if (roots.Count == 0)
        {
            return null;
        }

        foreach (var path in EnumerateAllowedLocalFiles(roots, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path))
            {
                continue;
            }

            var info = new FileInfo(path);
            if (info.Length <= 0)
            {
                continue;
            }

            var pathContentId = $"path:{slskd.Compute.Sha256Hash($"{path}|{info.Length}")}";
            if (!string.Equals(contentId, pathContentId, StringComparison.Ordinal))
            {
                continue;
            }

            return new ResolvedContent(path, info.Length, GetContentType(path));
        }

        return null;
    }

    private IReadOnlyList<string> GetAllowedLocalRoots()
    {
        var options = _options!.CurrentValue;
        var roots = options.Shares.Directories
            .Select(raw => new Share(raw))
            .Where(share => !share.IsExcluded)
            .Select(share => share.LocalPath)
            .Concat(new[] { options.Directories.Downloads })
            .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return roots;
    }

    private static IEnumerable<string> EnumerateAllowedLocalFiles(
        IReadOnlyList<string> roots,
        CancellationToken cancellationToken)
    {
        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (var file in files.Take(MaxFallbackFilesToScan))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return file;
            }
        }
    }

    private bool IsAllowedLocalPath(string path)
    {
        if (_options == null)
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path);
        return GetAllowedLocalRoots().Any(root =>
            fullPath.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || string.Equals(fullPath, root, StringComparison.Ordinal));
    }

    private static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext)) return "application/octet-stream";
        return MimeByExtension.TryGetValue(ext.ToLowerInvariant(), out var mime) ? mime : "application/octet-stream";
    }

    private static readonly IReadOnlyDictionary<string, string> MimeByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".flac"] = "audio/flac",
        [".mp3"] = "audio/mpeg",
        [".m4a"] = "audio/mp4",
        [".aac"] = "audio/aac",
        [".ogg"] = "audio/ogg",
        [".opus"] = "audio/opus",
        [".wav"] = "audio/wav",
        [".webm"] = "video/webm",
        [".mp4"] = "video/mp4",
        [".mkv"] = "video/x-matroska",
        [".avi"] = "video/x-msvideo",
        [".mov"] = "video/quicktime",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".txt"] = "text/plain",
        [".pdf"] = "application/pdf",
    };
}
