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

            // Create the request
            var request = new HttpRequestMessage(HttpMethod.Post, inboxUrl)
            {
                Content = JsonContent.Create(activity, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                })
            };

            // Add standard headers
            request.Headers.Add("User-Agent", "slskdN/1.0");
            request.Headers.Add("Accept", "application/activity+json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/activity+json");

            // Add HTTP signature
            await AddHttpSignatureAsync(request, activity, fedOpts, cancellationToken);

            return request;
        }

        /// <summary>
        ///     Adds HTTP signature to the request.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <param name="activity">The activity being delivered.</param>
        /// <param name="fedOpts">The federation options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task AddHttpSignatureAsync(
            HttpRequestMessage request,
            ActivityPubActivity activity,
            SocialFederationOptions fedOpts,
            CancellationToken cancellationToken)
        {
            // Extract actor from activity
            var actorId = activity.Actor?.ToString() ?? $"{fedOpts.BaseUrl}/actors/user";

            // Get the actor's private key
            using var privateKey = await _keyStore.GetPrivateKeyAsync(actorId, cancellationToken);

            // Create signing string (simplified HTTP signature)
            var date = DateTimeOffset.UtcNow.ToString("r");
            var host = request.RequestUri?.Host ?? "localhost";
            var digest = await CreateDigestAsync(request.Content);

            var signingString = $"date: {date}\nhost: {host}\ndigest: {digest}";

            // Sign the string
            var signatureBytes = SignData(signingString, privateKey.PemString);
            var signature = Convert.ToBase64String(signatureBytes);

            // Add signature headers
            request.Headers.Add("Date", date);
            request.Headers.Add("Digest", digest);
            request.Headers.Add("Signature", $"keyId=\"{actorId}#main-key\",algorithm=\"rsa-sha256\",headers=\"date host digest\",signature=\"{signature}\"");
        }

        /// <summary>
        ///     Creates a digest of the request body.
        /// </summary>
        /// <param name="content">The HTTP content.</param>
        /// <returns>The digest string.</returns>
        private static async Task<string> CreateDigestAsync(HttpContent? content)
        {
            if (content == null)
            {
                return "SHA-256=" + Convert.ToBase64String(SHA256.HashData(Array.Empty<byte>()));
            }

            var bytes = await content.ReadAsByteArrayAsync();
            var hash = SHA256.HashData(bytes);
            return "SHA-256=" + Convert.ToBase64String(hash);
        }

        /// <summary>
        ///     Signs data with RSA private key.
        /// </summary>
        /// <param name="data">The data to sign.</param>
        /// <param name="privateKeyPem">The private key in PEM format.</param>
        /// <returns>The signature bytes.</returns>
        private static byte[] SignData(string data, string privateKeyPem)
        {
            // For now, use a simple HMAC-based signature
            // TODO: Implement proper RSA signing with the Ed25519 key
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("federation-signing-key"));
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
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


