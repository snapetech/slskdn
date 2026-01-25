// <copyright file="ServicePayloadParser.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Text;
using slskd.Mesh.Transport;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// Safe JSON parsing for mesh service call payloads (size and depth limits).
/// </summary>
public static class ServicePayloadParser
{
    /// <summary>
    /// Parses JSON from <see cref="ServiceCall.Payload"/> with size and depth limits.
    /// Uses <see cref="SecurityUtils.MaxRemotePayloadSize"/> when no explicit max is provided.
    /// </summary>
    /// <returns>(value, null) on success; (default, ServiceReply) on failure.</returns>
    public static (T? value, ServiceReply? error) TryParseJson<T>(ServiceCall call)
        where T : class
        => TryParseJson<T>(call, SecurityUtils.MaxRemotePayloadSize);

    /// <summary>
    /// Parses JSON from <see cref="ServiceCall.Payload"/> with the given max size and depth limits.
    /// </summary>
    /// <param name="call">The service call.</param>
    /// <param name="maxPayloadSize">Maximum payload size in bytes (from <see cref="Mesh.MeshSecurityOptions"/>).</param>
    /// <returns>(value, null) on success; (default, ServiceReply) on failure.</returns>
    public static (T? value, ServiceReply? error) TryParseJson<T>(ServiceCall call, int maxPayloadSize)
        where T : class
    {
        if (call.Payload == null || call.Payload.Length == 0)
        {
            return (default, new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "Payload required"
            });
        }

        if (call.Payload.Length > maxPayloadSize)
        {
            return (default, new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.PayloadTooLarge,
                ErrorMessage = "Payload too large"
            });
        }

        try
        {
            var json = Encoding.UTF8.GetString(call.Payload);
            var v = SecurityUtils.ParseJsonSafely<T>(json, maxPayloadSize, SecurityUtils.MaxParseDepth);
            return (v, null);
        }
        catch (ArgumentException)
        {
            return (default, new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "Invalid JSON"
            });
        }
        catch (System.Text.Json.JsonException)
        {
            return (default, new ServiceReply
            {
                CorrelationId = call.CorrelationId,
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "Invalid JSON"
            });
        }
    }
}
