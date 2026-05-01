// <copyright file="SourceFeedImportModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SourceFeeds;

public sealed class SourceFeedImportRequest
{
    public string SourceText { get; init; } = string.Empty;

    public string SourceKind { get; init; } = "auto";

    public string ProviderAccessToken { get; init; } = string.Empty;

    public bool IncludeAlbum { get; init; }

    public bool FetchProviderUrls { get; init; } = true;

    public int Limit { get; init; } = 500;
}

public sealed class SourceFeedImportResult
{
    public string Provider { get; init; } = "local";

    public string SourceKind { get; init; } = "auto";

    public string SourceId { get; init; } = string.Empty;

    public int TotalRows { get; set; }

    public int SuggestionCount { get; set; }

    public int DuplicateCount { get; set; }

    public int SkippedCount { get; set; }

    public int NetworkRequestCount { get; set; }

    public bool RequiresAccessToken { get; init; }

    public string RequiredScopeHint { get; init; } = string.Empty;

    public List<SourceFeedSuggestion> Suggestions { get; init; } = [];

    public List<SourceFeedSkippedRow> SkippedRows { get; init; } = [];
}

public sealed class SourceFeedImportHistoryEntry
{
    public string ImportId { get; init; } = string.Empty;

    public DateTimeOffset ImportedAt { get; init; }

    public string Provider { get; init; } = "local";

    public string SourceKind { get; init; } = "auto";

    public string SourceId { get; init; } = string.Empty;

    public string SourceFingerprint { get; init; } = string.Empty;

    public string SourcePreview { get; init; } = string.Empty;

    public int Limit { get; init; }

    public bool IncludeAlbum { get; init; }

    public bool FetchProviderUrls { get; init; }

    public int TotalRows { get; init; }

    public int SuggestionCount { get; init; }

    public int DuplicateCount { get; init; }

    public int SkippedCount { get; init; }

    public int NetworkRequestCount { get; init; }

    public bool RequiresAccessToken { get; init; }

    public string RequiredScopeHint { get; init; } = string.Empty;

    public List<SourceFeedSuggestion> Suggestions { get; init; } = [];

    public List<SourceFeedSkippedRow> SkippedRows { get; init; } = [];
}

public sealed class SourceFeedSuggestion
{
    public string Title { get; init; } = string.Empty;

    public string Artist { get; init; } = string.Empty;

    public string Album { get; init; } = string.Empty;

    public string SearchText { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string SourceId { get; init; } = string.Empty;

    public string SourceItemId { get; init; } = string.Empty;

    public string ProviderUrl { get; init; } = string.Empty;

    public string EvidenceKey { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}

public sealed class SourceFeedSkippedRow
{
    public int RowNumber { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string RawText { get; init; } = string.Empty;
}
