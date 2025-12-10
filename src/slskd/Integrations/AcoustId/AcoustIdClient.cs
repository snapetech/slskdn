// <copyright file="AcoustIdClient.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.AcoustId
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using slskd;
    using slskd.Integrations.AcoustId.Models;

    /// <summary>
    ///     AcoustID API client implementation.
    /// </summary>
    public class AcoustIdClient : IAcoustIdClient
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly IOptionsMonitor<slskd.Options> optionsMonitor;
        private readonly ILogger<AcoustIdClient> log;
        private readonly JsonSerializerOptions serializerOptions;

        public AcoustIdClient(IHttpClientFactory httpClientFactory, IOptionsMonitor<slskd.Options> optionsMonitor, ILogger<AcoustIdClient> log)
        {
            this.httpClientFactory = httpClientFactory;
            this.optionsMonitor = optionsMonitor;
            this.log = log;
            serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        private slskd.Options.IntegrationOptions.AcoustIdOptions AcoustIdOptions => optionsMonitor.CurrentValue.Integration.AcoustId;

        public async Task<AcoustIdResult?> LookupAsync(string fingerprint, int sampleRate, int durationSeconds, CancellationToken cancellationToken = default)
        {
            var options = AcoustIdOptions;

            if (!options.Enabled)
            {
                return null;
            }

            var client = httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{options.BaseUrl.TrimEnd('/')}/lookup");

            var payload = new Dictionary<string, string>
            {
                ["client"] = options.ClientId,
                ["format"] = "json",
                ["fingerprint"] = fingerprint,
                ["duration"] = durationSeconds.ToString(CultureInfo.InvariantCulture),
                ["sample_rate"] = sampleRate.ToString(CultureInfo.InvariantCulture),
                ["meta"] = "recordings",
            };

            request.Content = new FormUrlEncodedContent(payload);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("AcoustID lookup failed ({StatusCode}) for fingerprint {Fingerprint}", response.StatusCode, fingerprint);
                response.EnsureSuccessStatusCode();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var root = await JsonSerializer.DeserializeAsync<AcoustIdRoot>(stream, serializerOptions, cancellationToken).ConfigureAwait(false);
            if (root?.Results == null || root.Results.Length == 0)
            {
                log.LogDebug("AcoustID returned no results for fingerprint {Fingerprint}", fingerprint);
                return null;
            }

            return root.Results[0];
        }
    }
}


