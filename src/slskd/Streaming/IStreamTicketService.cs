// <copyright file="IStreamTicketService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Streaming;

/// <summary>
/// Creates short-lived opaque stream tickets for browser media element playback.
/// </summary>
public interface IStreamTicketService
{
    /// <summary>
    /// Creates a ticket bound to a content id and owner key.
    /// </summary>
    string Create(string contentId, string ownerKey, TimeSpan lifetime);

    /// <summary>
    /// Validates a ticket for the requested content id.
    /// </summary>
    StreamTicketClaims? Validate(string ticket, string contentId);
}

public sealed record StreamTicketClaims(string ContentId, string OwnerKey, DateTimeOffset ExpiresAtUtc);
