// <copyright file="PerceptualHashController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.MediaCore.API.Controllers;

using slskd.Core.Security;

/// <summary>
/// Perceptual hash computation API controller.
/// </summary>
[Route("api/v0/mediacore/perceptualhash")]
[ApiController]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class PerceptualHashController : ControllerBase
{
    private readonly ILogger<PerceptualHashController> _logger;
    private readonly IPerceptualHasher _hasher;

    public PerceptualHashController(
        ILogger<PerceptualHashController> logger,
        IPerceptualHasher hasher)
    {
        _logger = logger;
        _hasher = hasher;
    }

    /// <summary>
    /// Computes perceptual hash for audio data.
    /// </summary>
    /// <param name="request">Audio hash computation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Computed perceptual hash</returns>
    [HttpPost("audio")]
    public Task<IActionResult> ComputeAudioHash([FromBody] AudioHashRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Samples == null || request.Samples.Length == 0)
        {
            return Task.FromResult<IActionResult>(BadRequest("Audio samples are required"));
        }

        if (request.SampleRate <= 0)
        {
            return Task.FromResult<IActionResult>(BadRequest("Valid sample rate is required"));
        }

        try
        {
            var algorithmValue = request.Algorithm ?? "ChromaPrint";
            if (!Enum.TryParse(algorithmValue, ignoreCase: true, out PerceptualHashAlgorithm algorithm))
            {
                return Task.FromResult<IActionResult>(BadRequest("Unsupported algorithm"));
            }

            var hash = _hasher.ComputeAudioHash(request.Samples, request.SampleRate, algorithm);

            _logger.LogInformation(
                "[PerceptualHash] Computed {Algorithm} hash for {SampleCount} samples at {SampleRate}Hz",
                algorithm, request.Samples.Length, request.SampleRate);

            return Task.FromResult<IActionResult>(Ok(hash));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PerceptualHash] Failed to compute audio hash");
            return Task.FromResult<IActionResult>(StatusCode(500, new { error = "Failed to compute audio perceptual hash" }));
        }
    }

    /// <summary>
    /// Computes perceptual hash for image data.
    /// </summary>
    /// <param name="request">Image hash computation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Computed perceptual hash</returns>
    [HttpPost("image")]
    public Task<IActionResult> ComputeImageHash([FromBody] ImageHashRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Pixels == null || request.Pixels.Length == 0)
        {
            return Task.FromResult<IActionResult>(BadRequest("Image pixels are required"));
        }

        if (request.Width <= 0 || request.Height <= 0)
        {
            return Task.FromResult<IActionResult>(BadRequest("Valid image dimensions are required"));
        }

        try
        {
            var algorithmValue = request.Algorithm ?? "PHash";
            if (!Enum.TryParse(algorithmValue, ignoreCase: true, out PerceptualHashAlgorithm algorithm))
            {
                return Task.FromResult<IActionResult>(BadRequest("Unsupported algorithm"));
            }

            var hash = _hasher.ComputeImageHash(request.Pixels, request.Width, request.Height, algorithm);

            _logger.LogInformation(
                "[PerceptualHash] Computed {Algorithm} hash for {Width}x{Height} image",
                algorithm, request.Width, request.Height);

            return Task.FromResult<IActionResult>(Ok(hash));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PerceptualHash] Failed to compute image hash");
            return Task.FromResult<IActionResult>(StatusCode(500, new { error = "Failed to compute image perceptual hash" }));
        }
    }

    /// <summary>
    /// Computes similarity between two perceptual hashes.
    /// </summary>
    /// <param name="request">Similarity computation request</param>
    /// <returns>Similarity analysis result</returns>
    [HttpPost("similarity")]
    public IActionResult ComputeSimilarity([FromBody] SimilarityRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.HashA) || string.IsNullOrWhiteSpace(request.HashB))
        {
            return BadRequest("Two hash values are required");
        }

        if (request.Threshold is < 0 or > 1)
        {
            return BadRequest("Threshold must be between 0 and 1");
        }

        try
        {
            var normalizedHashA = NormalizeHexHash(request.HashA);
            var normalizedHashB = NormalizeHexHash(request.HashB);

            if (!ulong.TryParse(normalizedHashA, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hashA) ||
                !ulong.TryParse(normalizedHashB, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hashB))
            {
                return BadRequest("Hash values must be valid hexadecimal numbers");
            }

            var distance = _hasher.HammingDistance(hashA, hashB);
            var similarity = _hasher.Similarity(hashA, hashB);
            var areSimilar = _hasher.AreSimilar(hashA, hashB, request.Threshold);

            return Ok(new
            {
                hashA = normalizedHashA,
                hashB = normalizedHashB,
                hammingDistance = distance,
                similarity,
                areSimilar,
                threshold = request.Threshold
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PerceptualHash] Failed to compute hash similarity");
            return StatusCode(500, new { error = "Failed to compute hash similarity" });
        }
    }

    /// <summary>
    /// Gets supported hash algorithms.
    /// </summary>
    /// <returns>List of supported algorithms</returns>
    [HttpGet("algorithms")]
    public IActionResult GetSupportedAlgorithms()
    {
        var algorithms = Enum.GetNames<PerceptualHashAlgorithm>();
        return Ok(new
        {
            algorithms,
            descriptions = new Dictionary<string, string>
            {
                ["ChromaPrint"] = "Audio fingerprinting algorithm for music identification",
                ["PHash"] = "Perceptual hash for image/video similarity detection",
                ["Spectral"] = "Simple spectral analysis hash (fallback)"
            }
        });
    }

    private static string NormalizeHexHash(string hash)
    {
        var normalized = hash.Trim();
        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }

        return normalized;
    }
}

/// <summary>
/// Audio hash computation request.
/// </summary>
public record AudioHashRequest(
    float[] Samples,
    int SampleRate,
    string? Algorithm = null);

/// <summary>
/// Image hash computation request.
/// </summary>
public record ImageHashRequest(
    byte[] Pixels,
    int Width,
    int Height,
    string? Algorithm = null);

/// <summary>
/// Hash similarity computation request.
/// </summary>
public record SimilarityRequest(
    string HashA,
    string HashB,
    double Threshold = 0.8);
