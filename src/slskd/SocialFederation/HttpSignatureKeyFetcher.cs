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
            keyId = keyId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(keyId))
                return null;

            if (!Uri.TryCreate(keyId, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("[HttpSignatureKeyFetcher] keyId must be HTTPS: {KeyId}", keyId);
                return null;
            }

            // Resolve host to IP and reject forbidden ranges
            if (!await IsSafeHostAsync(uri, cancellationToken))
            {
                _logger.LogDebug("[HttpSignatureKeyFetcher] keyId host resolves to forbidden or unreadable IP: {Host}", uri.Host);
                return null;
            }

            // Fetch without following redirects to unsafe hosts; use a client that limits redirects and size
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            req.Headers.Add("Accept", "application/activity+json");
            req.Headers.Add("Accept", "application/ld+json");

            try
            {
                using var res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                res.EnsureSuccessStatusCode();

                if (res.Content.Headers.ContentLength is long contentLength && contentLength > MaxResponseBytes)
                {
                    _logger.LogDebug("[HttpSignatureKeyFetcher] Response too large from {KeyId}", keyId);
                    return null;
                }

                var finalUri = res.RequestMessage?.RequestUri;
                if (finalUri == null ||
                    !string.Equals(finalUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("[HttpSignatureKeyFetcher] Final fetch URI was not HTTPS for {KeyId}", keyId);
                    return null;
                }

                if (!await IsSafeHostAsync(finalUri, cts.Token))
                {
                    _logger.LogDebug("[HttpSignatureKeyFetcher] Final fetch URI resolved to forbidden host for {KeyId}", keyId);
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
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[HttpSignatureKeyFetcher] Fetch failed for {KeyId}", keyId);
                return null;
            }
        }

        private async Task<bool> IsSafeHostAsync(Uri uri, CancellationToken cancellationToken)
        {
            var host = uri.DnsSafeHost?.Trim();
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            if (IPAddress.TryParse(host, out var parsedAddress))
            {
                return !(IPAddress.IsLoopback(parsedAddress) || IsLinkLocal(parsedAddress) || IsPrivate(parsedAddress) || IsMulticast(parsedAddress));
            }

            try
            {
                var addrs = await Dns.GetHostAddressesAsync(host, cancellationToken);
                if (addrs.Length == 0)
                {
                    return false;
                }

                foreach (var a in addrs)
                {
                    if (IPAddress.IsLoopback(a) || IsLinkLocal(a) || IsPrivate(a) || IsMulticast(a))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[HttpSignatureKeyFetcher] DNS resolution failed for {Host}", uri.Host);
                return false;
            }
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
                return ExtractPublicKeyPkix(doc.RootElement, keyId);
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? ExtractPublicKeyPkix(JsonElement element, string keyId)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    if (TryExtractPkixFromKeyObject(element, keyId, out var pkix))
                    {
                        return pkix;
                    }

                    if (element.TryGetProperty("publicKey", out var publicKey))
                    {
                        return ExtractPublicKeyPkix(publicKey, keyId);
                    }

                    return null;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        var nestedPkix = ExtractPublicKeyPkix(item, keyId);
                        if (nestedPkix != null)
                        {
                            return nestedPkix;
                        }
                    }

                    return null;

                default:
                    return null;
            }
        }

        private static bool TryExtractPkixFromKeyObject(JsonElement element, string keyId, out byte[]? pkix)
        {
            pkix = null;

            if (!TryGetStringProperty(element, "publicKeyPem", out var pem))
            {
                return false;
            }

            if (!TryGetStringProperty(element, "id", out var id) || !KeyIdsMatch(id, keyId))
            {
                return false;
            }

            pkix = PemToBytes(pem);
            return true;
        }

        private static bool TryGetStringProperty(JsonElement element, string propertyName, out string value)
        {
            value = string.Empty;
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = property.GetString()?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool KeyIdsMatch(string actual, string expected)
        {
            actual = actual.Trim();
            expected = expected.Trim();

            if (Uri.TryCreate(actual, UriKind.Absolute, out var actualUri) &&
                Uri.TryCreate(expected, UriKind.Absolute, out var expectedUri))
            {
                return Uri.Compare(actualUri, expectedUri, UriComponents.AbsoluteUri, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;
            }

            return string.Equals(actual, expected, StringComparison.Ordinal);
        }

        private static byte[] PemToBytes(string pem)
        {
            var lines = pem.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var b64 = new System.Collections.Generic.List<string>();
            var inKey = false;
            foreach (var line in lines)
            {
                if (line.Contains("-----BEGIN", StringComparison.Ordinal))
                {
                    inKey = true;
                    continue;
                }

                if (line.Contains("-----END", StringComparison.Ordinal)) break;
                if (inKey) b64.Add(line.Trim());
            }

            return Convert.FromBase64String(string.Concat(b64));
        }
    }
}
