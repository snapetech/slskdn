using MessagePack;
using System;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// DTO for service RPC requests.
/// Sent from client to server over the mesh overlay.
/// </summary>
[MessagePackObject]
public sealed record ServiceCall
{
    /// <summary>
    /// Service name to invoke (e.g., "pods", "shadow-index").
    /// </summary>
    [Key(0)]
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Method/operation name within the service (e.g., "Join", "PostMessage", "LookupByMbId").
    /// </summary>
    [Key(1)]
    public string Method { get; init; } = string.Empty;

    /// <summary>
    /// Unique correlation ID for matching request to response.
    /// </summary>
    [Key(2)]
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Serialized request payload (method-specific data).
    /// </summary>
    [Key(3)]
    public byte[] Payload { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// When this call was created (UTC, Unix milliseconds).
    /// </summary>
    [Key(4)]
    public long TimestampUnixMs { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

/// <summary>
/// DTO for service RPC responses.
/// Sent from server back to client over the mesh overlay.
/// </summary>
[MessagePackObject]
public sealed record ServiceReply
{
    /// <summary>
    /// Correlation ID matching the original ServiceCall.
    /// </summary>
    [Key(0)]
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Status code: 0 = success, nonzero = error.
    /// Common codes:
    /// 0 = OK
    /// 1 = Unknown error
    /// 2 = Service not found
    /// 3 = Method not found
    /// 4 = Invalid payload
    /// 5 = Timeout
    /// 6 = Rate limited
    /// 7 = Unauthorized
    /// 8 = Payload too large
    /// </summary>
    [Key(1)]
    public int StatusCode { get; init; }

    /// <summary>
    /// Serialized response payload (method-specific data) or error details.
    /// </summary>
    [Key(2)]
    public byte[] Payload { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// Optional error message (human-readable, safe for logging).
    /// </summary>
    [Key(3)]
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// When this reply was created (UTC, Unix milliseconds).
    /// </summary>
    [Key(4)]
    public long TimestampUnixMs { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Helper: Check if this reply indicates success.
    /// </summary>
    [IgnoreMember]
    public bool IsSuccess => StatusCode == 0;
}

/// <summary>
/// Standard status codes for ServiceReply.
/// </summary>
public static class ServiceStatusCodes
{
    public const int OK = 0;
    public const int UnknownError = 1;
    public const int ServiceNotFound = 2;
    public const int MethodNotFound = 3;
    public const int InvalidPayload = 4;
    public const int Timeout = 5;
    public const int RateLimited = 6;
    public const int Unauthorized = 7;
    public const int PayloadTooLarge = 8;
    
    /// <summary>
    /// Service unavailable (9) - Circuit breaker open or service degraded.
    /// </summary>
    public const int ServiceUnavailable = 9;
}

