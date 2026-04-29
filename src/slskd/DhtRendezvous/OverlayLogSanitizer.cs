// <copyright file="OverlayLogSanitizer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.DhtRendezvous;

using System.Net;
using slskd.Common.Security;
using slskd.Mesh.Transport;

internal static class OverlayLogSanitizer
{
    public static string Username(string? username) => LoggingSanitizer.SanitizeExternalIdentifier(username);

    public static string PeerId(string? peerId) => LoggingSanitizer.SanitizeExternalIdentifier(peerId);

    public static string Endpoint(EndPoint? endpoint) => endpoint is null
        ? "[null]"
        : LoggingUtils.SafeEndpoint(endpoint.ToString());

    public static string Endpoint(IPAddress? address) => address is null
        ? "[null]"
        : LoggingUtils.SafeEndpoint(address.ToString());
}
