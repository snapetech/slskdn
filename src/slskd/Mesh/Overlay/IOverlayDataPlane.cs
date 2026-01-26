// <copyright file="IOverlayDataPlane.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.Overlay;

/// <summary>
/// Data-plane overlay for bulk payloads over QUIC.
/// </summary>
public interface IOverlayDataPlane
{
    Task<bool> SendAsync(byte[] payload, IPEndPoint endpoint, CancellationToken ct = default);

    /// <summary>
    /// Opens a bidirectional QUIC stream to the given endpoint. Caller must dispose the stream.
    /// Used by RelayOnlyTransport for TCP relay over the data overlay.
    /// </summary>
    /// <returns>The stream, or null if the overlay is disabled or QUIC is not supported.</returns>
    Task<Stream?> OpenBidirectionalStreamAsync(IPEndPoint endpoint, CancellationToken ct = default);
}
