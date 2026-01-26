// <copyright file="ContentLocator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Streaming;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using slskd.Shares;

/// <summary>
/// Resolves contentId via the local IShareRepository (FindContentItem → FindFileInfo). Path comes from originalFilename (local path).
/// Never uses client-supplied paths. Enforces IsAdvertisable.
/// </summary>
public sealed class ContentLocator : IContentLocator
{
    private readonly IShareService _shareService;
    private readonly ILogger<ContentLocator> _log;

    public ContentLocator(IShareService shareService, ILogger<ContentLocator> log)
    {
        _shareService = shareService ?? throw new ArgumentNullException(nameof(shareService));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <inheritdoc />
    public ResolvedContent? Resolve(string contentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
            return null;

        var repo = _shareService.GetLocalRepository();
        var ci = repo.FindContentItem(contentId);
        if (ci == null || !ci.Value.IsAdvertisable)
        {
            _log.LogDebug("[ContentLocator] ContentId {ContentId} not found or not advertisable", contentId);
            return null;
        }

        var finfo = repo.FindFileInfo(ci.Value.MaskedFilename);
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
