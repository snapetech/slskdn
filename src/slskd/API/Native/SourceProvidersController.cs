// <copyright file="SourceProvidersController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.API.Native;

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using slskd.Core.Security;
using slskd.VirtualSoulfind.Core;
using slskd.VirtualSoulfind.v2.Backends;

/// <summary>
/// Provides a read-only source provider capability catalogue.
/// </summary>
[ApiController]
[Route("api/source-providers")]
[Route("api/v{version:apiVersion}/source-providers")]
[ApiVersion("0")]
[Produces("application/json")]
[ValidateCsrfForCookiesOnly]
public class SourceProvidersController : ControllerBase
{
    private static readonly IReadOnlyList<ProviderDefinition> ProviderDefinitions =
    [
        new(
            ContentBackendType.LocalLibrary,
            "Local Library",
            "Already indexed or shared files on this slskdN node.",
            "local",
            ContentDomain.Music,
            ["search", "download", "checksum", "metadata", "preview"],
            false,
            "No peer traffic. Uses local catalogue/share data only.",
            "Always available when acquisition planning is enabled."),
        new(
            ContentBackendType.Soulseek,
            "Soulseek",
            "Public Soulseek search and peer transfer path.",
            "public-network",
            ContentDomain.Music,
            ["search", "download", "preview", "metadata"],
            true,
            "Rate-limited searches, queue/speed filtering, and user-visible profile caps.",
            "Primary compatibility provider."),
        new(
            ContentBackendType.NativeMesh,
            "Native Mesh",
            "Trusted slskdN overlay peers with content descriptors.",
            "trusted-mesh",
            ContentDomain.Music,
            ["search", "download", "checksum", "preview"],
            false,
            "Trusted overlay only; no public peer probing.",
            "Disabled until mesh content publication is enabled."),
        new(
            ContentBackendType.MeshDht,
            "Mesh DHT",
            "DHT-backed hints for verified content candidates.",
            "trusted-mesh",
            ContentDomain.Music,
            ["search", "checksum", "preview"],
            false,
            "DHT operations must use rate limits and trust thresholds.",
            "Disabled until trusted mesh discovery is enabled."),
        new(
            ContentBackendType.Http,
            "HTTP",
            "User-configured HTTP or HTTPS repositories.",
            "configured-network",
            null,
            ["download", "checksum", "preview"],
            true,
            "Requires allowlisted hosts, size limits, and SSRF-safe fetches.",
            "Disabled until allowlisted repositories are configured."),
        new(
            ContentBackendType.WebDav,
            "WebDAV",
            "User-configured WebDAV repositories.",
            "configured-network",
            null,
            ["search", "download", "checksum", "preview", "auth"],
            true,
            "Requires allowlisted hosts, size limits, and configured credentials when needed.",
            "Disabled until allowlisted repositories are configured."),
        new(
            ContentBackendType.S3,
            "S3",
            "User-configured S3-compatible object storage.",
            "configured-network",
            null,
            ["search", "download", "checksum", "auth"],
            true,
            "Requires bucket allowlists, object size limits, and configured credentials.",
            "Disabled until allowlisted buckets are configured."),
        new(
            ContentBackendType.Lan,
            "LAN",
            "User-configured local network shares.",
            "configured-lan",
            null,
            ["search", "download", "checksum", "preview"],
            true,
            "Restricted to allowed local-network roots.",
            "Disabled until LAN roots are configured."),
        new(
            ContentBackendType.Torrent,
            "Private Torrent",
            "Explicitly configured private torrent or magnet sources.",
            "high-risk",
            null,
            ["search", "download", "checksum"],
            true,
            "High-risk provider. Must remain disabled until configured and explicitly enabled.",
            "Disabled by default."),
    ];

    private static readonly IReadOnlyList<ProfileProviderPolicyResponse> ProfilePolicies =
    [
        new(
            "lossless-exact",
            "Lossless Exact",
            ["LocalLibrary", "Soulseek", "NativeMesh", "MeshDht"],
            false,
            "Prefer exact local and public-network music matches before trusted mesh fallback."),
        new(
            "fast-good-enough",
            "Fast Good Enough",
            ["LocalLibrary", "Soulseek"],
            false,
            "Prefer quick local or public-network candidates with bounded result collection."),
        new(
            "album-complete",
            "Album Complete",
            ["LocalLibrary", "Soulseek", "NativeMesh", "MeshDht"],
            false,
            "Prefer folder-level candidates and trusted hash evidence after public search."),
        new(
            "rare-hunt",
            "Rare Hunt",
            ["LocalLibrary", "Soulseek", "NativeMesh", "MeshDht", "Http", "WebDav", "S3"],
            false,
            "Allow broader configured-source review while keeping every acquisition manual."),
        new(
            "conservative-network",
            "Conservative Network",
            ["LocalLibrary", "Soulseek"],
            false,
            "Keep public-network breadth narrow and avoid configured-source fallback by default."),
        new(
            "mesh-preferred",
            "Mesh Preferred",
            ["LocalLibrary", "NativeMesh", "MeshDht", "Soulseek"],
            false,
            "Prefer trusted mesh candidates, then fall back to public Soulseek compatibility."),
        new(
            "metadata-strict",
            "Metadata Strict",
            ["LocalLibrary", "Soulseek", "NativeMesh", "MeshDht"],
            false,
            "Prefer sources that can be explained and verified before import."),
    ];

    private readonly IReadOnlySet<ContentBackendType> registeredBackends;
    private readonly IOptionsSnapshot<slskd.Options> options;

    public SourceProvidersController(
        IEnumerable<IContentBackend> contentBackends,
        IOptionsSnapshot<slskd.Options> options)
    {
        registeredBackends = contentBackends
            .Select(backend => backend.Type)
            .ToHashSet();
        this.options = options;
    }

    /// <summary>
    /// Gets known source providers and their capability/risk metadata.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AuthPolicy.Any)]
    public IActionResult Get()
    {
        var v2Enabled = options.Value.VirtualSoulfindV2.Enabled;
        var providers = ProviderDefinitions
            .Select(definition => ToResponse(definition, v2Enabled))
            .OrderBy(provider => provider.SortOrder)
            .ThenBy(provider => provider.Name)
            .ToList();

        return Ok(new SourceProviderCatalogResponse(v2Enabled, providers, ProfilePolicies));
    }

    private SourceProviderResponse ToResponse(ProviderDefinition definition, bool v2Enabled)
    {
        var registered = registeredBackends.Contains(definition.Type);
        var active = v2Enabled && registered && IsEnabledByDefault(definition.Type);
        var disabledReason = active
            ? null
            : GetDisabledReason(definition, v2Enabled, registered);

        return new SourceProviderResponse(
            Id: definition.Type.ToString(),
            Name: definition.Name,
            Description: definition.Description,
            RiskLevel: definition.RiskLevel,
            Domain: definition.Domain?.ToString() ?? "Any",
            Capabilities: definition.Capabilities,
            RequiresConfiguration: definition.RequiresConfiguration,
            Registered: registered,
            Active: active,
            NetworkPolicy: definition.NetworkPolicy,
            DisabledReason: disabledReason,
            SortOrder: GetSortOrder(definition.Type));
    }

    private static bool IsEnabledByDefault(ContentBackendType type)
    {
        return type is ContentBackendType.LocalLibrary or ContentBackendType.Soulseek;
    }

    private static string GetDisabledReason(ProviderDefinition definition, bool v2Enabled, bool registered)
    {
        if (!v2Enabled)
        {
            return "VirtualSoulfind v2 acquisition planning is disabled.";
        }

        if (!registered)
        {
            return "Provider service is not registered in this build.";
        }

        return definition.DefaultDisabledReason;
    }

    private static int GetSortOrder(ContentBackendType type)
    {
        return type switch
        {
            ContentBackendType.LocalLibrary => 0,
            ContentBackendType.Soulseek => 10,
            ContentBackendType.NativeMesh => 20,
            ContentBackendType.MeshDht => 30,
            ContentBackendType.Http => 40,
            ContentBackendType.WebDav => 50,
            ContentBackendType.S3 => 60,
            ContentBackendType.Lan => 70,
            ContentBackendType.Torrent => 90,
            _ => 100,
        };
    }

    private sealed record ProviderDefinition(
        ContentBackendType Type,
        string Name,
        string Description,
        string RiskLevel,
        ContentDomain? Domain,
        IReadOnlyList<string> Capabilities,
        bool RequiresConfiguration,
        string NetworkPolicy,
        string DefaultDisabledReason);
}

public sealed record SourceProviderCatalogResponse(
    bool AcquisitionPlanningEnabled,
    IReadOnlyList<SourceProviderResponse> Providers,
    IReadOnlyList<ProfileProviderPolicyResponse> ProfilePolicies);

public sealed record SourceProviderResponse(
    string Id,
    string Name,
    string Description,
    string RiskLevel,
    string Domain,
    IReadOnlyList<string> Capabilities,
    bool RequiresConfiguration,
    bool Registered,
    bool Active,
    string NetworkPolicy,
    string? DisabledReason,
    int SortOrder);

public sealed record ProfileProviderPolicyResponse(
    string ProfileId,
    string ProfileName,
    IReadOnlyList<string> ProviderPriority,
    bool AutoDownloadEnabled,
    string Notes);
