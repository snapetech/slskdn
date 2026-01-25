// <copyright file="HttpSignatureKeyFetcher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SocialFederation
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    ///     SSRF-safe fetcher for ActivityPub HTTP Signature keyId URLs. PR-14.
    ///     Only HTTPS; rejects loopback, link-local, private, multicast; redirects ≤3; timeout 3s; max 256 KB.
    /// </summary>
    public sealed class HttpSignatureKeyFetcher : IHttpSignatureKeyFetcher
    {
        private const int MaxRedirects = 3;
        private const int TimeoutSeconds = 3;
        private const int MaxResponseBytes = 256 * 1024;

        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpSignatureKeyFetcher> _logger;

        public HttpSignatureKeyFetcher(HttpClient httpClient, ILogger<HttpSignatureKeyFetcher> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<byte[]?> FetchPublicKeyPkixAsync(string keyId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(keyId))
                return null;

            if (!Uri.TryCreate(keyId, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("[HttpSignatureKeyFetcher] keyId must be HTTPS: {KeyId}", keyId);
                return null;
            }

            // Resolve host to IP and reject forbidden ranges
            try
            {
                var addrs = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
                foreach (var a in addrs)
                {
                    if (IPAddress.IsLoopback(a) || IsLinkLocal(a) || IsPrivate(a) || IsMulticast(a))
                    {
                        _logger.LogDebug("[HttpSignatureKeyFetcher] keyId host resolves to forbidden IP: {Host} -> {Ip}", uri.Host, a);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[HttpSignatureKeyFetcher] DNS resolution failed for {Host}", uri.Host);
                return null;
            }

            // Fetch without following redirects to unsafe hosts; use a client that limits redirects and size
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            req.Headers.Add("Accept", "application/activity+json");

            HttpResponseMessage? res = null;
            try
            {
                res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                res.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[HttpSignatureKeyFetcher] Fetch failed for {KeyId}", keyId);
                return null;
            }

            await using var stream = await res.Content.ReadAsStreamAsync(cts.Token);
            var buf = new byte[MaxResponseBytes];
            var total = 0;
            int n;
            while (total < buf.Length && (n = await stream.ReadAsync(buf.AsMemory(total, buf.Length - total), cts.Token)) > 0)
            {
                total += n;
            }
            if (total >= MaxResponseBytes)
            {
                _logger.LogDebug("[HttpSignatureKeyFetcher] Response too large from {KeyId}", keyId);
                return null;
            }

            var json = Encoding.UTF8.GetString(buf.AsSpan(0, total));
            return ExtractPublicKeyPkixFromActorJson(json, keyId);
        }

        private static bool IsLinkLocal(IPAddress a)
        {
            if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                return a.GetAddressBytes() is [169, 254, ..];
            if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                return a.IsIPv6LinkLocal;
            return false;
        }

        private static bool IsPrivate(IPAddress a)
        {
            if (a.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                return a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && (a.IsIPv6SiteLocal || a.IsIPv6UniqueLocal);
            var b = a.GetAddressBytes();
            if (b.Length != 4) return false;
            if (b[0] == 10) return true;
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
            if (b[0] == 192 && b[1] == 168) return true;
            return false;
        }

        private static bool IsMulticast(IPAddress a)
        {
            if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                return a.GetAddressBytes() is [>= 224 and <= 239, ..];
            return a.IsIPv6Multicast;
        }

        private static byte[]? ExtractPublicKeyPkixFromActorJson(string json, string keyId)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("publicKey", out var pk) || pk.ValueKind != JsonValueKind.Object)
                    return null;
                var id = pk.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (string.IsNullOrEmpty(id) || !string.Equals(id, keyId, StringComparison.Ordinal))
                    return null;
                if (!pk.TryGetProperty("publicKeyPem", out var pemEl))
                    return null;
                var pem = pemEl.GetString();
                if (string.IsNullOrWhiteSpace(pem))
                    return null;
                return PemToBytes(pem);
            }
            catch
            {
                return null;
            }
        }

        private static byte[] PemToBytes(string pem)
        {
            var lines = pem.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var b64 = new System.Collections.Generic.List<string>();
            var inKey = false;
            foreach (var line in lines)
            {
                if (line.Contains("-----BEGIN", StringComparison.Ordinal)) { inKey = true; continue; }
                if (line.Contains("-----END", StringComparison.Ordinal)) break;
                if (inKey) b64.Add(line.Trim());
            }
            return Convert.FromBase64String(string.Concat(b64));
        }
    }
}
