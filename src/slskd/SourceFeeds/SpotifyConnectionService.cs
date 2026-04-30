// <copyright file="SpotifyConnectionService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SourceFeeds;

using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

public sealed class SpotifyConnectionService : ISpotifyConnectionService
{
    public const string RequiredScopes = "user-library-read user-follow-read playlist-read-private playlist-read-collaborative";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object _storageSync = new();
    private readonly ConcurrentDictionary<string, PendingAuthorization> _pending = new(StringComparer.Ordinal);
    private readonly string _storagePath;
    private readonly IDataProtector _protector;
    private SpotifyTokenStore? _store;

    public SpotifyConnectionService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<global::slskd.Options> optionsMonitor,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SpotifyConnectionService> logger)
    {
        HttpClientFactory = httpClientFactory;
        OptionsMonitor = optionsMonitor;
        Logger = logger;
        _protector = dataProtectionProvider.CreateProtector("slskdn.source-feeds.spotify-token-store.v1");
        _storagePath = Path.Combine(
            string.IsNullOrWhiteSpace(global::slskd.Program.AppDirectory)
                ? global::slskd.Program.DefaultAppDirectory
                : global::slskd.Program.AppDirectory,
            "source-feeds",
            "spotify-connection.json");
        LoadState();
    }

    private IHttpClientFactory HttpClientFactory { get; }

    private IOptionsMonitor<global::slskd.Options> OptionsMonitor { get; }

    private ILogger<SpotifyConnectionService> Logger { get; }

    public SpotifyConnectionStatus GetStatus()
    {
        var options = OptionsMonitor.CurrentValue.Integration.Spotify;
        lock (_storageSync)
        {
            return new SpotifyConnectionStatus
            {
                Configured = options.Enabled && !string.IsNullOrWhiteSpace(options.ClientId),
                Connected = _store != null && !string.IsNullOrWhiteSpace(_store.RefreshToken),
                DisplayName = _store?.DisplayName ?? string.Empty,
                SpotifyUserId = _store?.SpotifyUserId ?? string.Empty,
                Scope = _store?.Scope ?? string.Empty,
                ExpiresAt = _store?.ExpiresAt,
            };
        }
    }

    public SpotifyAuthorizationStart BeginAuthorization(string redirectUri)
    {
        var options = OptionsMonitor.CurrentValue.Integration.Spotify;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ClientId))
        {
            throw new InvalidOperationException("Spotify integration requires integrations.spotify.enabled and integrations.spotify.client_id.");
        }

        var state = GenerateBase64Url(32);
        var verifier = GenerateCodeVerifier();
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        _pending[state] = new PendingAuthorization(verifier, redirectUri, DateTimeOffset.UtcNow.AddMinutes(10));

        var parameters = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = options.ClientId,
            ["scope"] = RequiredScopes,
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = challenge,
        };

        return new SpotifyAuthorizationStart
        {
            AuthorizationUrl = $"https://accounts.spotify.com/authorize?{BuildQuery(parameters)}",
            RedirectUri = redirectUri,
            Scope = RequiredScopes,
        };
    }

    public async Task<SpotifyConnectionStatus> CompleteAuthorizationAsync(
        string state,
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        if (!_pending.TryRemove(state, out var pending) ||
            pending.ExpiresAt <= DateTimeOffset.UtcNow ||
            !string.Equals(pending.RedirectUri, redirectUri, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Spotify authorization state is invalid or expired.");
        }

        var options = OptionsMonitor.CurrentValue.Integration.Spotify;
        var token = await RequestTokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = pending.CodeVerifier,
        }, cancellationToken).ConfigureAwait(false);

        var profile = await GetProfileAsync(token.AccessToken, cancellationToken).ConfigureAwait(false);
        lock (_storageSync)
        {
            _store = new SpotifyTokenStore
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                Scope = token.Scope,
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, token.ExpiresIn - 30)),
                DisplayName = profile?.DisplayName ?? string.Empty,
                SpotifyUserId = profile?.Id ?? string.Empty,
            };
            PersistState();
        }

        return GetStatus();
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        SpotifyTokenStore? snapshot;
        lock (_storageSync)
        {
            snapshot = _store;
        }

        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.RefreshToken))
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.AccessToken) && snapshot.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return snapshot.AccessToken;
        }

        var options = OptionsMonitor.CurrentValue.Integration.Spotify;
        var token = await RequestTokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = snapshot.RefreshToken,
        }, cancellationToken).ConfigureAwait(false);

        lock (_storageSync)
        {
            if (_store == null)
            {
                return token.AccessToken;
            }

            _store = _store with
            {
                AccessToken = token.AccessToken,
                RefreshToken = string.IsNullOrWhiteSpace(token.RefreshToken) ? _store.RefreshToken : token.RefreshToken,
                Scope = string.IsNullOrWhiteSpace(token.Scope) ? _store.Scope : token.Scope,
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, token.ExpiresIn - 30)),
            };
            PersistState();
        }

        return token.AccessToken;
    }

    public void Disconnect()
    {
        lock (_storageSync)
        {
            _store = null;
            if (File.Exists(_storagePath))
            {
                File.Delete(_storagePath);
            }
        }
    }

    private async Task<SpotifyTokenResponse> RequestTokenAsync(
        Dictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
        {
            Content = new FormUrlEncodedContent(form),
        };

        using var response = await SendSpotifyAsync(request, cancellationToken).ConfigureAwait(false);
        var token = await response.Content.ReadFromJsonAsync<SpotifyTokenResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
        return token ?? new SpotifyTokenResponse();
    }

    private async Task<SpotifyProfile?> GetProfileAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await SendSpotifyAsync(request, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<SpotifyProfile>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendSpotifyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var options = OptionsMonitor.CurrentValue.Integration.Spotify;
        var client = HttpClientFactory.CreateClient(nameof(SpotifyConnectionService));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        var response = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private void LoadState()
    {
        lock (_storageSync)
        {
            if (!File.Exists(_storagePath))
            {
                return;
            }

            try
            {
                var protectedJson = File.ReadAllText(_storagePath);
                var json = _protector.Unprotect(protectedJson);
                _store = JsonSerializer.Deserialize<SpotifyTokenStore>(json, JsonOptions);
            }
            catch (IOException ex)
            {
                Logger.LogWarning(ex, "[SourceFeeds] Failed to load Spotify connection state from {Path}", _storagePath);
            }
            catch (JsonException ex)
            {
                Logger.LogWarning(ex, "[SourceFeeds] Failed to parse Spotify connection state from {Path}", _storagePath);
            }
            catch (CryptographicException ex)
            {
                Logger.LogWarning(ex, "[SourceFeeds] Failed to decrypt Spotify connection state from {Path}", _storagePath);
            }
        }
    }

    private void PersistState()
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var protectedJson = _protector.Protect(JsonSerializer.Serialize(_store, JsonOptions));
        var tempPath = $"{_storagePath}.tmp";
        File.WriteAllText(tempPath, protectedJson);
        File.Move(tempPath, _storagePath, overwrite: true);
    }

    private static string GenerateCodeVerifier()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
        var bytes = RandomNumberGenerator.GetBytes(64);
        var builder = new StringBuilder(bytes.Length);
        foreach (var value in bytes)
        {
            builder.Append(alphabet[value % alphabet.Length]);
        }

        return builder.ToString();
    }

    private static string GenerateBase64Url(int byteCount)
        => Base64UrlEncode(RandomNumberGenerator.GetBytes(byteCount));

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string BuildQuery(Dictionary<string, string> parameters)
        => string.Join("&", parameters.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

    private sealed record PendingAuthorization(string CodeVerifier, string RedirectUri, DateTimeOffset ExpiresAt);

    private sealed record SpotifyTokenStore
    {
        public string AccessToken { get; init; } = string.Empty;

        public string RefreshToken { get; init; } = string.Empty;

        public string Scope { get; init; } = string.Empty;

        public DateTimeOffset ExpiresAt { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        public string SpotifyUserId { get; init; } = string.Empty;
    }

    private sealed record SpotifyTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("scope")]
        public string Scope { get; init; } = string.Empty;
    }

    private sealed record SpotifyProfile
    {
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string DisplayName { get; init; } = string.Empty;
    }
}
