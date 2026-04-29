// <copyright file="ContentBackendType.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Backends
{
    /// <summary>
    ///     Enum representing the type of content backend.
    /// </summary>
    /// <remarks>
    ///     VirtualSoulfind v2 supports multiple backends for content acquisition.
    ///     Different content domains (Music, Video, Book) may restrict which backends are allowed.
    /// </remarks>
    public enum ContentBackendType
    {
        /// <summary>
        ///     Local library (content already on disk).
        /// </summary>
        LocalLibrary,

        /// <summary>
        ///     Soulseek network (Music domain only).
        /// </summary>
        /// <remarks>
        ///     Subject to strict per-backend caps (H-08).
        ///     NOT allowed for Video, Book, or GenericFile domains.
        /// </remarks>
        Soulseek,

        /// <summary>
        ///     Mesh/DHT overlay (multi-source swarm).
        /// </summary>
        MeshDht,

        /// <summary>
        ///     BitTorrent / multi-swarm.
        /// </summary>
        Torrent,

        /// <summary>
        ///     HTTP/HTTPS sources (catalogues, CDN, etc.).
        /// </summary>
        /// <remarks>
        ///     Must use SSRF-safe HTTP client.
        ///     Domain allowlists required.
        /// </remarks>
        Http,

        /// <summary>
        ///     LAN sources (local network shares, etc.).
        /// </summary>
        Lan,

        /// <summary>
        ///     Native mesh overlay only (no Soulseek, no BitTorrent).
        /// </summary>
        /// <remarks>
        ///     Find candidates via mesh/DHT (e.g. IMeshDirectory.FindPeersByContentAsync),
        ///     fetch via overlay transfer. Use case: mesh-only deployments, legacy fallback, closed communities.
        /// </remarks>
        NativeMesh,

        /// <summary>
        ///     WebDAV (PROPFIND, GET). Remote storage over HTTP.
        /// </summary>
        /// <remarks>
        ///     Domain allowlist; optional Basic/Bearer auth. BackendRef = full WebDAV URL.
        /// </remarks>
        WebDav,

        /// <summary>
        ///     S3-compatible object storage (MinIO, AWS S3, Backblaze B2, etc.).
        /// </summary>
        /// <remarks>
        ///     ListObjectsV2, GetObject. BackendRef = s3://bucket/key. Auth: access/secret or IAM.
        /// </remarks>
        S3,
    }
}
