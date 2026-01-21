// <copyright file="VirtualSoulfindValidation.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Core
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

    /// <summary>
    ///     Validation utilities for VirtualSoulfind input validation and domain gating.
    /// </summary>
    /// <remarks>
    ///     H-VF01: VirtualSoulfind Input Validation & Domain Gating.
    ///     Ensures ContentDomain is properly validated and domain isolation is enforced.
    /// </remarks>
    public static class VirtualSoulfindValidation
    {
        /// <summary>
        ///     Validates that a ContentDomain value is supported and within allowed ranges.
        /// </summary>
        /// <param name="domain">The domain to validate.</param>
        /// <returns>True if the domain is valid and supported.</returns>
        public static bool IsValidContentDomain(ContentDomain domain)
        {
            // Only Music and GenericFile domains are currently supported
            return domain == ContentDomain.Music || domain == ContentDomain.GenericFile;
        }

        /// <summary>
        ///     Validates that a ContentDomain value is supported and within allowed ranges.
        /// </summary>
        /// <param name="domain">The domain to validate.</param>
        /// <param name="errorMessage">Output error message if validation fails.</param>
        /// <returns>True if the domain is valid and supported.</returns>
        public static bool IsValidContentDomain(ContentDomain domain, out string? errorMessage)
        {
            errorMessage = null;

            if (!IsValidContentDomain(domain))
            {
                errorMessage = $"ContentDomain '{domain}' is not supported. Supported domains: {string.Join(", ", GetSupportedDomains())}";
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Gets the list of currently supported content domains.
        /// </summary>
        /// <returns>Array of supported ContentDomain values.</returns>
        public static ContentDomain[] GetSupportedDomains()
        {
            return new[] { ContentDomain.Music, ContentDomain.GenericFile };
        }

        /// <summary>
        ///     Validates that the specified domain is allowed to use Soulseek backends.
        /// </summary>
        /// <param name="domain">The domain to check.</param>
        /// <returns>True if the domain can use Soulseek backends.</returns>
        /// <remarks>
        ///     Only Music domain is allowed to use Soulseek backends for network health reasons.
        ///     GenericFile domain must use mesh/DHT/local backends only.
        /// </remarks>
        public static bool CanDomainUseSoulseek(ContentDomain domain)
        {
            return domain == ContentDomain.Music;
        }

        /// <summary>
        ///     Validates that required fields are present based on the content domain.
        /// </summary>
        /// <param name="domain">The content domain.</param>
        /// <param name="trackId">The track identifier (required for Music domain).</param>
        /// <param name="fileHash">The file hash (required for GenericFile domain).</param>
        /// <param name="fileSize">The file size (required for GenericFile domain).</param>
        /// <param name="errorMessage">Output error message if validation fails.</param>
        /// <returns>True if all required fields for the domain are present.</returns>
        public static bool ValidateRequiredFields(
            ContentDomain domain,
            string? trackId,
            string? fileHash,
            long? fileSize,
            out string? errorMessage)
        {
            errorMessage = null;

            switch (domain)
            {
                case ContentDomain.Music:
                    if (string.IsNullOrWhiteSpace(trackId))
                    {
                        errorMessage = "TrackId is required for Music domain";
                        return false;
                    }
                    break;

                case ContentDomain.GenericFile:
                    if (string.IsNullOrWhiteSpace(fileHash))
                    {
                        errorMessage = "FileHash is required for GenericFile domain";
                        return false;
                    }
                    if (!fileSize.HasValue || fileSize.Value <= 0)
                    {
                        errorMessage = "FileSize is required and must be positive for GenericFile domain";
                        return false;
                    }
                    break;

                default:
                    errorMessage = $"Unsupported domain: {domain}";
                    return false;
            }

            return true;
        }

        /// <summary>
        ///     Validates that a track ID has the correct format for the specified domain.
        /// </summary>
        /// <param name="domain">The content domain.</param>
        /// <param name="trackId">The track identifier to validate.</param>
        /// <param name="errorMessage">Output error message if validation fails.</param>
        /// <returns>True if the track ID format is valid for the domain.</returns>
        public static bool ValidateTrackIdFormat(
            ContentDomain domain,
            string trackId,
            out string? errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(trackId))
            {
                errorMessage = "TrackId cannot be null or empty";
                return false;
            }

            // For Music domain, track ID should be a valid MBID or UUID format
            if (domain == ContentDomain.Music)
            {
                // Basic validation: should be a UUID format (MBIDs are UUIDs)
                if (!Guid.TryParse(trackId, out _))
                {
                    errorMessage = "TrackId must be a valid UUID format for Music domain";
                    return false;
                }
            }

            // For GenericFile domain, track ID can be any non-empty string
            // (it represents the file hash)

            return true;
        }

        /// <summary>
        ///     Validates that a file hash has the correct format for GenericFile domain.
        /// </summary>
        /// <param name="fileHash">The file hash to validate.</param>
        /// <param name="errorMessage">Output error message if validation fails.</param>
        /// <returns>True if the file hash format is valid.</returns>
        public static bool ValidateFileHashFormat(string fileHash, out string? errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(fileHash))
            {
                errorMessage = "FileHash cannot be null or empty";
                return false;
            }

            // SHA256 hash should be 64 characters of hexadecimal
            if (fileHash.Length != 64 || !fileHash.All(c => "0123456789abcdefABCDEF".Contains(c)))
            {
                errorMessage = "FileHash must be a valid SHA256 hash (64 hexadecimal characters)";
                return false;
            }

            return true;
        }
    }

    /// <summary>
    ///     Model validator for ContentDomain fields.
    /// </summary>
    public class ContentDomainValidator : IModelValidator
    {
        /// <inheritdoc/>
        public IEnumerable<ModelValidationResult> Validate(ModelValidationContext context)
        {
            var domain = (ContentDomain?)context.Model;
            if (!domain.HasValue)
            {
                yield return new ModelValidationResult(context.ModelMetadata.PropertyName, "ContentDomain is required");
                yield break;
            }

            if (!VirtualSoulfindValidation.IsValidContentDomain(domain.Value, out var errorMessage))
            {
                yield return new ModelValidationResult(context.ModelMetadata.PropertyName, errorMessage);
            }
        }
    }
}


