// <copyright file="S3Backend.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.VirtualSoulfind.v2.Backends
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Microsoft.Extensions.Options;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Sources;

    /// <summary>
    ///     Backend for S3-compatible object storage (AWS S3, MinIO, Backblaze B2).
    /// </summary>
    /// <remarks>
    ///     FindCandidates from registry; BackendRef = s3://bucket/key. Validate via HeadObject.
    ///     Options: Endpoint (for MinIO/B2), Region, AccessKey, SecretKey, ForcePathStyle.
    /// </remarks>
    public sealed class S3Backend : IContentFetchBackend
    {
        private readonly IOptionsMonitor<S3BackendOptions> _options;
        private readonly ISourceRegistry _sourceRegistry;

        public S3Backend(
            IOptionsMonitor<S3BackendOptions> options,
            ISourceRegistry sourceRegistry)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _sourceRegistry = sourceRegistry ?? throw new ArgumentNullException(nameof(sourceRegistry));
        }

        public ContentBackendType Type => ContentBackendType.S3;

        public ContentDomain? SupportedDomain => null;

        /// <summary>
        ///     Find S3 candidates from source registry. Filters by bucket allowlist when configured.
        /// </summary>
        public async Task<IReadOnlyList<SourceCandidate>> FindCandidatesAsync(
            ContentItemId itemId,
            CancellationToken cancellationToken = default)
        {
            var opts = _options.CurrentValue;
            if (!opts.Enabled)
            {
                return Array.Empty<SourceCandidate>();
            }

            var candidates = await _sourceRegistry.FindCandidatesForItemAsync(
                itemId,
                ContentBackendType.S3,
                cancellationToken);

            if (opts.BucketAllowlist != null && opts.BucketAllowlist.Count > 0)
            {
                var allow = new HashSet<string>(opts.BucketAllowlist, StringComparer.OrdinalIgnoreCase);
                return candidates
                    .Where(c => TryParseRef(c.BackendRef, out var b, out _) && allow.Contains(b))
                    .ToList();
            }

            return candidates.ToList();
        }

        /// <summary>
        ///     Validate S3 candidate: parse s3://bucket/key, HeadObject, size limits.
        /// </summary>
        public async Task<SourceCandidateValidationResult> ValidateCandidateAsync(
            SourceCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            if (candidate.Backend != ContentBackendType.S3)
            {
                return SourceCandidateValidationResult.Invalid("Not an S3 candidate");
            }

            if (!TryParseRef(candidate.BackendRef, out var bucket, out var key))
            {
                return SourceCandidateValidationResult.Invalid("Invalid BackendRef; expected s3://bucket/key");
            }

            if (string.IsNullOrEmpty(key))
            {
                return SourceCandidateValidationResult.Invalid("Missing key in s3://bucket/key");
            }

            var opts = _options.CurrentValue;
            if (!opts.Enabled)
            {
                return SourceCandidateValidationResult.Invalid("S3 backend disabled");
            }

            if (opts.BucketAllowlist != null && opts.BucketAllowlist.Count > 0)
            {
                var allowed = opts.BucketAllowlist.Any(b =>
                    string.Equals(b, bucket, StringComparison.OrdinalIgnoreCase));
                if (!allowed)
                {
                    return SourceCandidateValidationResult.Invalid($"Bucket {bucket} not in allowlist");
                }
            }

            AmazonS3Client? client = null;
            try
            {
                client = CreateClient(opts);
                var request = new GetObjectMetadataRequest { BucketName = bucket, Key = key };
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(opts.ValidationTimeoutSeconds));

                var meta = await client.GetObjectMetadataAsync(request, cts.Token);

                var len = meta.ContentLength;
                if (len > opts.MaxFileSizeBytes)
                {
                    return SourceCandidateValidationResult.Invalid("File too large");
                }

                if (len == 0)
                {
                    return SourceCandidateValidationResult.Invalid("Empty object");
                }

                return SourceCandidateValidationResult.Valid(candidate.TrustScore, candidate.ExpectedQuality);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return SourceCandidateValidationResult.Invalid("Object not found");
            }
            catch (OperationCanceledException)
            {
                return SourceCandidateValidationResult.Invalid("Timeout");
            }
            catch (AmazonS3Exception ex)
            {
                return SourceCandidateValidationResult.Invalid($"S3 error: {ex.ErrorCode}");
            }
            finally
            {
                client?.Dispose();
            }
        }

        /// <inheritdoc />
        public async Task FetchToStreamAsync(
            SourceCandidate candidate,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            if (candidate.Backend != ContentBackendType.S3)
                throw new ArgumentException("Not an S3 candidate", nameof(candidate));

            if (!TryParseRef(candidate.BackendRef, out var bucket, out var key) || string.IsNullOrEmpty(bucket) || string.IsNullOrEmpty(key))
                throw new ArgumentException("Invalid BackendRef; expected s3://bucket/key", nameof(candidate));

            var opts = _options.CurrentValue;
            if (!opts.Enabled)
                throw new InvalidOperationException("S3 backend disabled");

            if (opts.BucketAllowlist != null && opts.BucketAllowlist.Count > 0)
            {
                if (!opts.BucketAllowlist.Any(b => string.Equals(b, bucket, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"Bucket {bucket} not in allowlist");
            }

            AmazonS3Client? client = null;
            try
            {
                client = CreateClient(opts);
                var request = new GetObjectRequest { BucketName = bucket, Key = key };
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(opts.ValidationTimeoutSeconds * 3, 60)));

                using var response = await client.GetObjectAsync(request, cts.Token);
                var len = response.ContentLength;
                if (len > opts.MaxFileSizeBytes)
                    throw new InvalidOperationException("File too large");
                if (len == 0)
                    throw new InvalidOperationException("Empty object");

                await using var src = response.ResponseStream;
                await CopyWithLimitAsync(src, destination, opts.MaxFileSizeBytes, cts.Token);
            }
            finally
            {
                client?.Dispose();
            }
        }

        private static async Task CopyWithLimitAsync(Stream source, Stream destination, long maxBytes, CancellationToken ct)
        {
            var buf = new byte[81920];
            long total = 0;
            int r;
            while ((r = await source.ReadAsync(buf, ct)) > 0)
            {
                total += r;
                if (total > maxBytes)
                    throw new InvalidOperationException("File too large");
                await destination.WriteAsync(buf.AsMemory(0, r), ct);
            }
        }

        private static AmazonS3Client CreateClient(S3BackendOptions opts)
        {
            var cfg = new AmazonS3Config
            {
                ForcePathStyle = opts.ForcePathStyle,
            };

            if (!string.IsNullOrEmpty(opts.Endpoint))
            {
                cfg.ServiceURL = opts.Endpoint.TrimEnd('/');
            }
            else if (!string.IsNullOrEmpty(opts.Region))
            {
                cfg.RegionEndpoint = RegionEndpoint.GetBySystemName(opts.Region);
            }
            else
            {
                cfg.RegionEndpoint = RegionEndpoint.USEast1;
            }

            if (!string.IsNullOrEmpty(opts.AccessKey) && !string.IsNullOrEmpty(opts.SecretKey))
            {
                return new AmazonS3Client(opts.AccessKey, opts.SecretKey, cfg);
            }

            return new AmazonS3Client(cfg);
        }

        private static bool TryParseRef(string? backendRef, out string? bucket, out string? key)
        {
            bucket = null;
            key = null;
            if (string.IsNullOrWhiteSpace(backendRef) || !backendRef.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var rest = backendRef.Substring(5);
            var idx = rest.IndexOf('/');
            if (idx < 0)
            {
                bucket = rest;
                key = string.Empty;
                return !string.IsNullOrEmpty(bucket);
            }

            bucket = rest.Substring(0, idx);
            key = rest.Substring(idx + 1);
            return !string.IsNullOrEmpty(bucket) && !string.IsNullOrEmpty(key);
        }
    }

    /// <summary>
    ///     Configuration for S3 backend.
    /// </summary>
    public sealed class S3BackendOptions
    {
        /// <summary>Enable S3 backend.</summary>
        public bool Enabled { get; init; } = false;

        /// <summary>Endpoint for S3-compatible services (MinIO, B2). Leave empty for AWS S3.</summary>
        public string? Endpoint { get; init; }

        /// <summary>AWS region when using AWS S3 (e.g. us-east-1).</summary>
        public string? Region { get; init; }

        /// <summary>Access key (required for MinIO/B2; optional for AWS with IAM/default).</summary>
        public string? AccessKey { get; init; }

        /// <summary>Secret key (required for MinIO/B2; optional for AWS).</summary>
        public string? SecretKey { get; init; }

        /// <summary>Force path-style for S3-compatible (MinIO). Default true when Endpoint is set.</summary>
        public bool ForcePathStyle { get; init; } = true;

        /// <summary>Bucket allowlist (optional). When set, only these buckets are accepted.</summary>
        public List<string>? BucketAllowlist { get; init; }

        /// <summary>Maximum object size (bytes).</summary>
        public long MaxFileSizeBytes { get; init; } = 500_000_000;

        /// <summary>HeadObject/GetObjectMetadata timeout (seconds).</summary>
        public int ValidationTimeoutSeconds { get; init; } = 15;
    }
}
