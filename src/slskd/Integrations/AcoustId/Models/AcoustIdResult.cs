// <copyright file="AcoustIdResult.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.AcoustId.Models
{
    using System.Text.Json.Serialization;

    public sealed class AcoustIdRoot
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("results")]
        public AcoustIdResult[] Results { get; set; }
    }

    public sealed class AcoustIdResult
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("recordings")]
        public Recording[] Recordings { get; set; }
    }

    public sealed class Recording
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("artists")]
        public Artist[] Artists { get; set; }
    }

    public sealed class Artist
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}



