// <copyright file="ProfileService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Identity;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Common.Security;
using slskd.Mesh.Transport;

/// <summary>Implementation of IProfileService. Stores own profile in a JSON file, signs/verifies with Ed25519.</summary>
public sealed class ProfileService : IProfileService
{
    private readonly Ed25519Signer _signer;
    private readonly IOptionsMonitor<slskd.Options> _options;
    private readonly ILogger<ProfileService> _log;
    private readonly IContactService? _contacts;
    private PeerProfile? _cachedMyProfile;
    private readonly object _cacheLock = new();

    public ProfileService(
        Ed25519Signer signer,
        IOptionsMonitor<slskd.Options> options,
        ILogger<ProfileService> log,
        IContactService? contacts = null)
    {
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _contacts = contacts;
    }

    private string ProfileFilePath
    {
        get
        {
            var dataDir = Program.AppDirectory;
            if (string.IsNullOrEmpty(dataDir))
                dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "slskd");
            Directory.CreateDirectory(dataDir);
            return Path.Combine(dataDir, "peer-profile.json");
        }
    }

    private (byte[] PrivateKey, byte[] PublicKey) GetOrCreateKeyPair()
    {
        var keyFile = Path.ChangeExtension(ProfileFilePath, ".key");
        if (File.Exists(keyFile))
        {
            try
            {
                var keyData = JsonSerializer.Deserialize<KeyPairData>(File.ReadAllText(keyFile));
                if (keyData != null && keyData.PrivateKey != null && keyData.PublicKey != null)
                {
                    return (Convert.FromBase64String(keyData.PrivateKey), Convert.FromBase64String(keyData.PublicKey));
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[ProfileService] Failed to load keypair from {KeyFile}, generating a new keypair", keyFile);
            }
        }

        var (priv, pub) = _signer.GenerateKeyPair();
        var keyPairData = new KeyPairData
        {
            PrivateKey = Convert.ToBase64String(priv),
            PublicKey = Convert.ToBase64String(pub)
        };
        File.WriteAllText(keyFile, JsonSerializer.Serialize(keyPairData, new JsonSerializerOptions { WriteIndented = true }));
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                File.SetUnixFileMode(keyFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[ProfileService] Could not set restrictive permissions on {KeyFile}", keyFile);
            }
        }

        _log.LogInformation("[ProfileService] Generated new Ed25519 keypair");
        return (priv, pub);
    }

    public async Task<PeerProfile> GetMyProfileAsync(CancellationToken ct = default)
    {
        lock (_cacheLock)
        {
            if (_cachedMyProfile != null) return _cachedMyProfile;
        }

        if (File.Exists(ProfileFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(ProfileFilePath, ct).ConfigureAwait(false);
                var profile = JsonSerializer.Deserialize<PeerProfile>(json);
                if (profile != null)
                {
                    // HARDENING-2026-04-20 H10: migrate pre-hardening profiles that auto-injected a
                    // LAN-IP Direct endpoint. Drop leaky endpoints, re-sign with our own key, and
                    // persist the scrubbed profile so the next serve returns a clean blob.
                    var scrubbed = StripLeakyEndpoints(profile.Endpoints, logContext: "on-disk profile load");
                    if (scrubbed.Count != profile.Endpoints.Count)
                    {
                        profile.Endpoints = scrubbed;
                        profile = SignProfile(profile);
                        await SaveMyProfileAsync(profile, ct).ConfigureAwait(false);
                    }

                    if (string.IsNullOrWhiteSpace(profile.DisplayName))
                    {
                        profile.DisplayName = GetDefaultDisplayName();
                        profile = SignProfile(profile);
                        await SaveMyProfileAsync(profile, ct).ConfigureAwait(false);
                    }

                    lock (_cacheLock) { _cachedMyProfile = profile; }
                    return profile;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[ProfileService] Failed to load profile from file");
            }
        }

        var (priv, pub) = GetOrCreateKeyPair();
        var peerId = Ed25519Signer.DerivePeerId(pub);
        var endpoints = new List<PeerEndpoint>();
        var webOpts = _options.CurrentValue.Web;
        if (webOpts.Port > 0)
        {
            var scheme = webOpts.Https?.Disabled != true ? "https" : "http";
            var host = DetectHostname();
            if (!string.IsNullOrEmpty(host))
            {
                var candidate = new PeerEndpoint { Type = "Direct", Address = $"{scheme}://{host}:{webOpts.Port}", Priority = 1 };
                if (!PeerEndpointPolicy.IsLeakyAddress(candidate.Address))
                {
                    endpoints.Add(candidate);
                }
                else
                {
                    // HARDENING-2026-04-20 H10: refuse to auto-publish a LAN IP / link-local / loopback as a
                    // public Direct endpoint. Operator must set a routable hostname explicitly if they want one.
                    _log.LogInformation(
                        "[ProfileService] H10: skipping auto-populated Direct endpoint '{Address}' " +
                        "because its host is private/reserved. Set a routable hostname via profile update if needed.",
                        candidate.Address);
                }
            }
        }

        var profile2 = new PeerProfile
        {
            PeerId = peerId,
            PublicKey = Convert.ToBase64String(pub),
            DisplayName = GetDefaultDisplayName(),
            Capabilities = 0,
            Endpoints = endpoints,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };
        profile2 = SignProfile(profile2);
        await SaveMyProfileAsync(profile2, ct).ConfigureAwait(false);
        return profile2;
    }

    private string GetDefaultDisplayName()
    {
        var username = _options.CurrentValue.Soulseek.Username;
        return string.IsNullOrWhiteSpace(username) ? "Unknown" : username.Trim();
    }

    public async Task<PeerProfile> UpdateMyProfileAsync(string displayName, string? avatar, int capabilities, List<PeerEndpoint> endpoints, CancellationToken ct = default)
    {
        var profile = await GetMyProfileAsync(ct).ConfigureAwait(false);
        profile.DisplayName = displayName;
        profile.Avatar = avatar;
        profile.Capabilities = capabilities;

        // HARDENING-2026-04-20 H10: strip any endpoint whose host is private/reserved before signing.
        // The profile is served anonymously; a naïvely-pasted internal URL would be publicly readable.
        profile.Endpoints = StripLeakyEndpoints(endpoints ?? new List<PeerEndpoint>(), logContext: "UpdateMyProfile");
        profile.ExpiresAt = DateTimeOffset.UtcNow.AddDays(7);
        profile = SignProfile(profile);
        await SaveMyProfileAsync(profile, ct).ConfigureAwait(false);
        lock (_cacheLock) { _cachedMyProfile = profile; }
        return profile;
    }

    private List<PeerEndpoint> StripLeakyEndpoints(List<PeerEndpoint> endpoints, string logContext)
    {
        if (endpoints == null || endpoints.Count == 0)
        {
            return new List<PeerEndpoint>();
        }

        var kept = new List<PeerEndpoint>(endpoints.Count);
        foreach (var endpoint in endpoints)
        {
            if (endpoint == null)
            {
                continue;
            }

            if (PeerEndpointPolicy.IsLeakyAddress(endpoint.Address))
            {
                _log.LogWarning(
                    "[ProfileService] H10: dropping peer endpoint '{Type} {Address}' during {Context} " +
                    "because its host is private/reserved/unparseable.",
                    endpoint.Type,
                    endpoint.Address,
                    logContext);
                continue;
            }

            kept.Add(endpoint);
        }

        return kept;
    }

    private async Task SaveMyProfileAsync(PeerProfile profile, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(ProfileFilePath, json, ct).ConfigureAwait(false);
    }

    public async Task<PeerProfile?> GetProfileAsync(string peerId, CancellationToken ct = default)
    {
        var myProfile = await GetMyProfileAsync(ct).ConfigureAwait(false);
        if (myProfile.PeerId == peerId)
        {
            return myProfile;
        }

        if (_contacts == null)
        {
            return null;
        }

        var contact = await _contacts.GetByPeerIdAsync(peerId, ct).ConfigureAwait(false);
        if (contact == null)
        {
            return null;
        }

        List<PeerEndpoint> endpoints = new();
        if (!string.IsNullOrWhiteSpace(contact.CachedEndpointsJson))
        {
            try
            {
                endpoints = JsonSerializer.Deserialize<List<PeerEndpoint>>(contact.CachedEndpointsJson) ?? new List<PeerEndpoint>();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[ProfileService] Failed to parse cached endpoints for peer {PeerId}", peerId);
            }
        }

        // HARDENING-2026-04-20 H10: contact-supplied endpoints are unsigned at this point (we
        // return Signature="" below), so stripping leaky ones here doesn't break any integrity
        // guarantee — and it prevents us from re-disclosing a friend's LAN IP to anonymous
        // callers of GET /profile/{peerId}.
        endpoints = StripLeakyEndpoints(endpoints, logContext: $"GetProfile({peerId})");

        return new PeerProfile
        {
            PeerId = contact.PeerId,
            DisplayName = string.IsNullOrWhiteSpace(contact.Nickname) ? contact.PeerId : contact.Nickname,
            Endpoints = endpoints,
            CreatedAt = contact.CreatedAt,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            Capabilities = 0,
            PublicKey = string.Empty,
            Signature = string.Empty
        };
    }

    public bool VerifyProfile(PeerProfile profile)
    {
        if (profile == null || string.IsNullOrEmpty(profile.Signature) || string.IsNullOrEmpty(profile.PublicKey))
            return false;

        try
        {
            var pubKey = Convert.FromBase64String(profile.PublicKey);
            var sig = Convert.FromBase64String(profile.Signature);
            var canonical = GetCanonicalJson(profile);
            var data = Encoding.UTF8.GetBytes(canonical);
            return _signer.Verify(data, sig, pubKey);
        }
        catch
        {
            return false;
        }
    }

    public PeerProfile SignProfile(PeerProfile profile)
    {
        var (priv, pub) = GetOrCreateKeyPair();
        if (string.IsNullOrEmpty(profile.PeerId))
            profile.PeerId = Ed25519Signer.DerivePeerId(pub);
        if (string.IsNullOrEmpty(profile.PublicKey))
            profile.PublicKey = Convert.ToBase64String(pub);

        var canonical = GetCanonicalJson(profile);
        var data = Encoding.UTF8.GetBytes(canonical);
        var sig = _signer.Sign(data, priv);
        profile.Signature = Convert.ToBase64String(sig);
        return profile;
    }

    private static string GetCanonicalJson(PeerProfile profile)
    {
        // Canonical JSON: sorted keys, no whitespace
        var dict = new Dictionary<string, object?>
        {
            ["peerId"] = profile.PeerId,
            ["publicKey"] = profile.PublicKey,
            ["displayName"] = profile.DisplayName,
            ["avatar"] = profile.Avatar,
            ["capabilities"] = profile.Capabilities,
            ["endpoints"] = profile.Endpoints.Select(e => new Dictionary<string, object>
            {
                ["type"] = e.Type,
                ["address"] = e.Address,
                ["priority"] = e.Priority
            }).ToList(),
            ["createdAt"] = profile.CreatedAt.ToString("O"),
            ["expiresAt"] = profile.ExpiresAt.ToString("O")
        };
        return JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = false });
    }

    public string GetFriendCode(string peerId)
    {
        // Encode first 10 bytes of PeerId (or hash) as Base32, format with dashes
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(peerId));
        var prefix = hash.Take(10).ToArray();
        var code = Base32Encode(prefix);
        return $"{code[..5]}-{code[5..9]}-{code[9..13]}-{code[13..16]}";
    }

    public string? DecodeFriendCode(string code)
    {
        var clean = code.Replace("-", string.Empty).ToUpperInvariant();
        if (clean.Length < 16) return null;

        try
        {
            var myProfile = GetMyProfileAsync().GetAwaiter().GetResult();
            if (string.Equals(GetFriendCode(myProfile.PeerId).Replace("-", string.Empty), clean, StringComparison.OrdinalIgnoreCase))
            {
                return myProfile.PeerId;
            }

            if (_contacts == null)
            {
                return null;
            }

            var contacts = _contacts.GetAllAsync().GetAwaiter().GetResult();
            foreach (var contact in contacts)
            {
                if (string.Equals(GetFriendCode(contact.PeerId).Replace("-", string.Empty), clean, StringComparison.OrdinalIgnoreCase))
                {
                    return contact.PeerId;
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[ProfileService] Failed to decode friend code");
        }

        return null;
    }

    // HARDENING-2026-04-20 H10: this used to return the first non-loopback IPv4 address — on a
    // home network, that's a LAN IP like 192.168.x.x, which then got auto-injected into the
    // anonymously-served PeerProfile as a Direct endpoint. Now we only return addresses that
    // pass PeerEndpointPolicy (public-routable). If nothing qualifies, return null and let the
    // caller skip the Direct endpoint entirely; the operator can add a routable hostname later.
    private string? DetectHostname()
    {
        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var ni in networkInterfaces)
            {
                var ipProperties = ni.GetIPProperties();
                foreach (var unicast in ipProperties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(unicast.Address) &&
                        unicast.Address.ToString() != "0.0.0.0" &&
                        !IpRangeClassifier.IsPrivate(unicast.Address) &&
                        !IpRangeClassifier.IsBlocked(unicast.Address))
                    {
                        return unicast.Address.ToString();
                    }
                }
            }

            try
            {
                var hostname = Dns.GetHostName();
                var hostEntry = Dns.GetHostEntry(hostname);
                var ipv4 = hostEntry.AddressList.FirstOrDefault(ip =>
                    ip.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(ip) &&
                    !IpRangeClassifier.IsPrivate(ip) &&
                    !IpRangeClassifier.IsBlocked(ip));
                if (ipv4 != null)
                {
                    return ipv4.ToString();
                }
            }
            catch
            {
                // Ignore DNS resolution errors
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[ProfileService] H10: Failed to detect hostname");
        }

        return null;
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder();
        var bits = 0;
        var bitCount = 0;
        foreach (var b in data)
        {
            bits = (bits << 8) | b;
            bitCount += 8;
            while (bitCount >= 5)
            {
                bitCount -= 5;
                result.Append(alphabet[(bits >> bitCount) & 31]);
            }
        }

        if (bitCount > 0)
            result.Append(alphabet[(bits << (5 - bitCount)) & 31]);
        return result.ToString();
    }

    private class KeyPairData
    {
        public string? PrivateKey { get; set; }
        public string? PublicKey { get; set; }
    }
}
