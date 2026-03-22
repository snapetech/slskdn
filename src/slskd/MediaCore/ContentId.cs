// <copyright file="ContentId.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;

namespace slskd.MediaCore;

/// <summary>
/// Represents a parsed ContentID with domain, type, and identifier components.
/// Format: content:domain:type:id
/// </summary>
public record ContentId(string Domain, string Type, string Id)
{
    /// <summary>
    /// Gets the full ContentID string representation.
    /// </summary>
    public string FullId => $"content:{Domain}:{Type}:{Id}";

    /// <summary>
    /// Gets whether this ContentID represents audio content.
    /// </summary>
    public bool IsAudio => ContentIdParser.NormalizeDomain(Domain, Type).Equals("audio", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether this ContentID represents video content.
    /// </summary>
    public bool IsVideo => ContentIdParser.NormalizeDomain(Domain, Type).Equals("video", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether this ContentID represents image content.
    /// </summary>
    public bool IsImage => ContentIdParser.NormalizeDomain(Domain, Type).Equals("image", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether this ContentID represents text content.
    /// </summary>
    public bool IsText => ContentIdParser.NormalizeDomain(Domain, Type).Equals("text", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether this ContentID represents application/binary content.
    /// </summary>
    public bool IsApplication => ContentIdParser.NormalizeDomain(Domain, Type).Equals("application", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Utility class for parsing and validating ContentID formats.
/// </summary>
public static class ContentIdParser
{
    // Regex pattern for ContentID format: content:<domain>:<type>:<id>
    private static readonly Regex ContentIdPattern = new(
        @"^content:([^:]+):([^:]+):(.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses a ContentID string into its components.
    /// </summary>
    /// <param name="contentId">The ContentID string to parse.</param>
    /// <returns>The parsed ContentId object, or null if parsing fails.</returns>
    public static ContentId? Parse(string contentId)
    {
        if (string.IsNullOrWhiteSpace(contentId))
            return null;

        var match = ContentIdPattern.Match(contentId.Trim());
        if (!match.Success || match.Groups.Count != 4)
            return null;

        var domain = match.Groups[1].Value;
        var type = match.Groups[2].Value;
        var id = match.Groups[3].Value;

        if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(id))
            return null;

        return new ContentId(domain, type, id);
    }

    /// <summary>
    /// Validates that a string is a properly formatted ContentID.
    /// </summary>
    /// <param name="contentId">The ContentID string to validate.</param>
    /// <returns>True if the ContentID is valid, false otherwise.</returns>
    public static bool IsValid(string contentId)
    {
        return Parse(contentId) != null;
    }

    /// <summary>
    /// Creates a ContentID string from domain, type, and id components.
    /// </summary>
    /// <param name="domain">The domain (e.g., "audio", "video").</param>
    /// <param name="type">The type within the domain (e.g., "track", "album", "movie").</param>
    /// <param name="id">The identifier within the type.</param>
    /// <returns>The formatted ContentID string.</returns>
    public static string Create(string domain, string type, string id)
    {
        if (string.IsNullOrWhiteSpace(domain))
            throw new ArgumentException("Domain cannot be empty", nameof(domain));
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Type cannot be empty", nameof(type));
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("ID cannot be empty", nameof(id));

        // Normalize to lowercase for consistency
        return $"content:{domain.ToLowerInvariant()}:{type.ToLowerInvariant()}:{id}";
    }

    /// <summary>
    /// Gets the domain from a ContentID string.
    /// </summary>
    /// <param name="contentId">The ContentID string.</param>
    /// <returns>The domain, or null if parsing fails.</returns>
    public static string? GetDomain(string contentId)
    {
        var parsed = Parse(contentId);
        return parsed == null ? null : NormalizeDomain(parsed.Domain, parsed.Type);
    }

    /// <summary>
    /// Gets the type from a ContentID string.
    /// </summary>
    /// <param name="contentId">The ContentID string.</param>
    /// <returns>The type, or null if parsing fails.</returns>
    public static string? GetType(string contentId)
    {
        var parsed = Parse(contentId);
        return parsed == null ? null : NormalizeType(parsed.Domain, parsed.Type);
    }

    /// <summary>
    /// Gets the identifier from a ContentID string.
    /// </summary>
    /// <param name="contentId">The ContentID string.</param>
    /// <returns>The identifier, or null if parsing fails.</returns>
    public static string? GetId(string contentId)
    {
        return Parse(contentId)?.Id;
    }

    public static string NormalizeDomain(string domain, string type)
    {
        if (domain.Equals("mb", StringComparison.OrdinalIgnoreCase))
        {
            return type.ToLowerInvariant() switch
            {
                "recording" or "release" or "artist" => ContentDomains.Audio,
                _ => domain.ToLowerInvariant(),
            };
        }

        return domain.ToLowerInvariant();
    }

    public static string NormalizeType(string domain, string type)
    {
        if (domain.Equals("mb", StringComparison.OrdinalIgnoreCase))
        {
            return type.ToLowerInvariant() switch
            {
                "recording" => ContentDomains.AudioTrack,
                "release" => ContentDomains.AudioAlbum,
                "artist" => ContentDomains.AudioArtist,
                _ => type.ToLowerInvariant(),
            };
        }

        return type.ToLowerInvariant();
    }
}

/// <summary>
/// Known content domains and types.
/// </summary>
public static class ContentDomains
{
    // Audio domain
    public const string Audio = "audio";
    public const string AudioTrack = "track";
    public const string AudioAlbum = "album";
    public const string AudioArtist = "artist";
    public const string AudioPlaylist = "playlist";

    // Video domain
    public const string Video = "video";
    public const string VideoMovie = "movie";
    public const string VideoSeries = "series";
    public const string VideoEpisode = "episode";
    public const string VideoClip = "clip";

    // Image domain
    public const string Image = "image";
    public const string ImagePhoto = "photo";
    public const string ImageArtwork = "artwork";
    public const string ImageScreenshot = "screenshot";

    // Text domain
    public const string Text = "text";
    public const string TextBook = "book";
    public const string TextArticle = "article";
    public const string TextDocument = "document";

    // Application domain
    public const string Application = "application";
    public const string ApplicationSoftware = "software";
    public const string ApplicationGame = "game";
    public const string ApplicationArchive = "archive";
}
