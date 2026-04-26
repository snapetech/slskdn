// <copyright file="WishlistCsvImport.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Wishlist;

using System;
using System.Collections.Generic;

public class WishlistCsvImportOptions
{
    public bool Enabled { get; set; } = true;

    public bool AutoDownload { get; set; }

    public int MaxResults { get; set; } = 100;

    public string Filter { get; set; } = string.Empty;

    public bool IncludeAlbum { get; set; }
}

public class WishlistCsvImportResult
{
    public int TotalRows { get; set; }

    public int CreatedCount { get; set; }

    public int DuplicateCount { get; set; }

    public int SkippedCount { get; set; }

    public List<WishlistItem> CreatedItems { get; set; } = [];

    public List<WishlistCsvImportSkippedRow> SkippedRows { get; set; } = [];
}

public class WishlistCsvImportSkippedRow
{
    public int RowNumber { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string RawText { get; set; } = string.Empty;
}

internal sealed class WishlistCsvTrack
{
    public int RowNumber { get; set; }

    public string SearchText { get; set; } = string.Empty;

    public string RawText { get; set; } = string.Empty;
}
