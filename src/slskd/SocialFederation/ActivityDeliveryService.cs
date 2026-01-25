// <copyright file="ActivityDeliveryService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SocialFederation
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NSec.Cryptography;

    /// <summary>
    ///     Service for delivering ActivityPub activities to remote servers.
    /// </summary>
    /// <remarks>
    ///     T-FED03: Activity delivery with HTTP signatures, rate limiting, and retry logic.
    ///     Handles fan-out delivery to multiple recipients with proper authentication.
    /// </remarks>
    public sealed class ActivityDeliveryService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly IOptionsMonitor<SocialFederationOptions> _federationOptions;
        private readonly IOptionsMonitor<FederationPublishingOptions> _publishingOptions;
        private readonly IActivityPubKeyStore _keyStore;
        private readonly ILogger<ActivityDeliveryService> _logger;
        private readonly SemaphoreSlim _rateLimiter;
        private readonly ConcurrentDictionary<string, DateTime> _recentDeliveries = new();
        private bool _disposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ActivityDeliveryService"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client for deliveries.</param>
        /// <param name="federationOptions">The federation options.</param>
        /// <param name="publishingOptions">The publishing options.</param>
        /// <param name="keyStore">The ActivityPub key store.</param>
        /// <param name="logger">The logger.</param>
        public ActivityDeliveryService(
            HttpClient httpClient,
            IOptionsMonitor<SocialFederationOptions> federationOptions,
            IOptionsMonitor<FederationPublishingOptions> publishingOptions,
            IActivityPubKeyStore keyStore,
            ILogger<ActivityDeliveryService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _federationOptions = federationOptions ?? throw new ArgumentNullException(nameof(federationOptions));
            _publishingOptions = publishingOptions ?? throw new ArgumentNullException(nameof(publishingOptions));
            _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var pubOpts = _publishingOptions.CurrentValue;
            _rateLimiter = new SemaphoreSlim(pubOpts.MaxActivitiesPerHour / 10, pubOpts.MaxActivitiesPerHour / 10);
            _httpClient.Timeout = TimeSpan.FromSeconds(pubOpts.DeliveryTimeoutSeconds);
        }

        /// <summary>
        ///     Delivers an activity to multiple recipients.
        /// </summary>
        /// <param name="activity">The activity to deliver.</param>
        /// <param name="recipientUrls">The recipient inbox URLs.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task DeliverActivityAsync(
            ActivityPubActivity activity,
            IEnumerable<string> recipientUrls,
            CancellationToken cancellationToken = default)
        {
            if (activity == null)
            {
                throw new ArgumentNullException(nameof(activity));
            }

            if (recipientUrls == null)
            {
                throw new ArgumentNullException(nameof(recipientUrls));
            }

            var pubOpts = _publishingOptions.CurrentValue;

            // Rate limiting
            await _rateLimiter.WaitAsync(cancellationToken);
            try
            {
                var deliveryTasks = recipientUrls
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .Distinct()
                    .Select(url => DeliverToRecipientAsync(activity, url, cancellationToken))
                    .ToList();

                await Task.WhenAll(deliveryTasks);
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        /// <summary>
        ///     Delivers an activity to a single recipient.
        /// </summary>
        /// <param name="activity">The activity to deliver.</param>
        /// <param name="inboxUrl">The recipient's inbox URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task DeliverToRecipientAsync(
            ActivityPubActivity activity,
            string inboxUrl,
            CancellationToken cancellationToken)
        {
            var pubOpts = _publishingOptions.CurrentValue;
            var maxRetries = pubOpts.MaxDeliveryRetries;

            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Check rate limiting for this specific recipient
                    if (IsRateLimited(inboxUrl))
                    {
                        _logger.LogDebug("[Delivery] Rate limited for {InboxUrl}, skipping", inboxUrl);
                        return;
                    }

                    // Create signed request
                    using var request = await CreateSignedRequestAsync(activity, inboxUrl, cancellationToken);

                    // Send the request
                    var response = await _httpClient.SendAsync(request, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    _logger.LogInformation("[Delivery] Successfully delivered activity {ActivityId} to {InboxUrl}",
                        activity.Id, inboxUrl);

                    // Record successful delivery for rate limiting
                    RecordDelivery(inboxUrl);
                    return;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "[Delivery] Failed to deliver activity to {InboxUrl} (attempt {Attempt}/{MaxRetries})",
                        inboxUrl, attempt + 1, maxRetries + 1);

                    // Exponential backoff
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Delivery] Permanently failed to deliver activity to {InboxUrl} after {MaxRetries} attempts",
                        inboxUrl, maxRetries + 1);
                }
            }
        }

        /// <summary>
        ///     Creates a signed HTTP request for activity delivery.
        /// </summary>
        /// <param name="activity">The activity to deliver.</param>
        /// <param name="inboxUrl">The inbox URL.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The signed HTTP request.</returns>
        private async Task<HttpRequestMessage> CreateSignedRequestAsync(
            ActivityPubActivity activity,
            string inboxUrl,
            CancellationToken cancellationToken)
        {
            var fedOpts = _federationOptions.CurrentValue;
            var jsonOpts = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            var bodyBytes = JsonSerializer.SerializeToUtf8Bytes(activity, jsonOpts);

            var request = new HttpRequestMessage(HttpMethod.Post, inboxUrl)
            {
                Content = new ByteArrayContent(bodyBytes)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/activity+json") }
                }
            };

            request.Headers.Add("User-Agent", "slskdN/1.0");
            request.Headers.Add("Accept", "application/activity+json");

            var actorId = activity.Actor?.ToString() ?? $"{fedOpts.BaseUrl}/actors/user";
            await AddHttpSignatureAsync(request, bodyBytes, actorId, fedOpts, cancellationToken);

            return request;
        }

        /// <summary>
        ///     Adds HTTP signature to the request. PR-14: Ed25519, (request-target), Digest.
        /// </summary>
        private async Task AddHttpSignatureAsync(
            HttpRequestMessage request,
            byte[] bodyBytes,
            string actorId,
            SocialFederationOptions fedOpts,
            CancellationToken cancellationToken)
        {
            using var disp = await _keyStore.GetPrivateKeyAsync(actorId, cancellationToken);
            var privateKeyPem = disp.PemString;

            var date = DateTimeOffset.UtcNow.ToString("r");
            var host = request.RequestUri?.Host ?? "localhost";
            var digest = "SHA-256=" + Convert.ToBase64String(SHA256.HashData(bodyBytes));

            var path = request.RequestUri?.AbsolutePath ?? "/";
            var requestTarget = $"(request-target): post {path}";
            var signingString = $"{requestTarget}\ndate: {date}\nhost: {host}\ndigest: {digest}";

            var signatureBytes = SignWithEd25519(Encoding.UTF8.GetBytes(signingString), privateKeyPem);
            var signature = Convert.ToBase64String(signatureBytes);

            request.Headers.Add("Date", date);
            request.Headers.Add("Digest", digest);
            request.Headers.Add("Signature", $"keyId=\"{actorId}#main-key\",algorithm=\"ed25519\",headers=\"(request-target) date host digest\",signature=\"{signature}\"");
        }

        /// <summary>
        ///     Signs data with Ed25519 private key from PEM (PKIX). PR-14.
        /// </summary>
        private static byte[] SignWithEd25519(byte[] data, string privateKeyPem)
        {
            var pkix = PemToBytes(privateKeyPem);
            var alg = SignatureAlgorithm.Ed25519;
            using var key = Key.Import(alg, pkix, KeyBlobFormat.PkixPrivateKey);
            return alg.Sign(key, data);
        }

        private static byte[] PemToBytes(string pem)
        {
            var lines = pem.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var b64 = new List<string>();
            var inKey = false;
            foreach (var line in lines)
            {
                if (line.Contains("-----BEGIN", StringComparison.Ordinal)) { inKey = true; continue; }
                if (line.Contains("-----END", StringComparison.Ordinal)) break;
                if (inKey) b64.Add(line.Trim());
            }
            return Convert.FromBase64String(string.Concat(b64));
        }

        /// <summary>
        ///     Checks if delivery to a recipient is currently rate limited.
        /// </summary>
        /// <param name="inboxUrl">The inbox URL.</param>
        /// <returns>True if rate limited.</returns>
        private bool IsRateLimited(string inboxUrl)
        {
            var pubOpts = _publishingOptions.CurrentValue;
            var cutoff = DateTime.UtcNow.AddMinutes(-60); // 1 hour window

            // Clean old entries
            foreach (var kvp in _recentDeliveries)
            {
                if (kvp.Value < cutoff)
                {
                    _recentDeliveries.TryRemove(kvp.Key, out _);
                }
            }

            // Count recent deliveries to this recipient
            var recentCount = _recentDeliveries.Count(kvp => kvp.Key == inboxUrl && kvp.Value > cutoff);
            return recentCount >= pubOpts.MaxActivitiesPerHour;
        }

        /// <summary>
        ///     Records a successful delivery for rate limiting.
        /// </summary>
        /// <param name="inboxUrl">The inbox URL.</param>
        private void RecordDelivery(string inboxUrl)
        {
            _recentDeliveries[inboxUrl] = DateTime.UtcNow;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _rateLimiter.Dispose();
            _disposed = true;
        }
    }
}


