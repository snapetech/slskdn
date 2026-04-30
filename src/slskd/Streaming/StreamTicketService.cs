// <copyright file="StreamTicketService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Streaming;

using System.Collections.Concurrent;
using System.Security.Cryptography;

/// <summary>
/// In-memory opaque ticket service for browser-managed stream requests.
/// </summary>
public sealed class StreamTicketService : IStreamTicketService
{
    private readonly ConcurrentDictionary<string, StreamTicketClaims> _tickets = new();

    public string Create(string contentId, string ownerKey, TimeSpan lifetime)
    {
        var ticket = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        _tickets[ticket] = new StreamTicketClaims(
            contentId,
            ownerKey,
            DateTimeOffset.UtcNow.Add(lifetime));

        return ticket;
    }

    public StreamTicketClaims? Validate(string ticket, string contentId)
    {
        if (string.IsNullOrWhiteSpace(ticket))
        {
            return null;
        }

        CleanupExpired();

        if (!_tickets.TryGetValue(ticket.Trim(), out var claims))
        {
            return null;
        }

        if (claims.ExpiresAtUtc <= DateTimeOffset.UtcNow ||
            !string.Equals(claims.ContentId, contentId, StringComparison.Ordinal))
        {
            if (claims.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                _tickets.TryRemove(ticket.Trim(), out _);
            }

            return null;
        }

        return claims;
    }

    private void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var item in _tickets)
        {
            if (item.Value.ExpiresAtUtc <= now)
            {
                _tickets.TryRemove(item.Key, out _);
            }
        }
    }
}
