// <copyright file="EnqueueDownloadBatchRequest.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.API;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

public record EnqueueDownloadBatchRequest
{
    public Guid? BatchId { get; init; }
    public Guid? SearchId { get; init; }

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Username { get; init; } = string.Empty;
    public List<EnqueueDownloadBatchItem> Files { get; init; } = [];
}

public record EnqueueDownloadBatchItem
{
    [Required]
    public string Filename { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public long Size { get; set; }
}
