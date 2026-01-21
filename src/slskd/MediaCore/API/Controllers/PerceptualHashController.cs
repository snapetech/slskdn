// <copyright file="PerceptualHashController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.MediaCore.API.Controllers;

/// <summary>
/// Perceptual hash computation API controller.
/// </summary>
[Route("api/v0/mediacore/perceptualhash")]
[ApiController]
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
    public async Task<IActionResult> ComputeAudioHash([FromBody] AudioHashRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Samples == null || request.Samples.Length == 0)
        {
            return BadRequest("Audio samples are required");
        }

        if (request.SampleRate <= 0)
        {
            return BadRequest("Valid sample rate is required");
        }

        try
        {
            var algorithm = Enum.Parse<PerceptualHashAlgorithm>(request.Algorithm ?? "ChromaPrint", ignoreCase: true);
            var hash = _hasher.ComputeAudioHash(request.Samples, request.SampleRate, algorithm);

            _logger.LogInformation(
                "[PerceptualHash] Computed {Algorithm} hash for {SampleCount} samples at {SampleRate}Hz",
                algorithm, request.Samples.Length, request.SampleRate);

            return Ok(hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PerceptualHash] Failed to compute audio hash");
            return StatusCode(500, new { error = "Failed to compute audio perceptual hash" });
        }
    }

    /// <summary>
    /// Computes perceptual hash for image data.
    /// </summary>
    /// <param name="request">Image hash computation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Computed perceptual hash</returns>
    [HttpPost("image")]
    public async Task<IActionResult> ComputeImageHash([FromBody] ImageHashRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Pixels == null || request.Pixels.Length == 0)
        {
            return BadRequest("Image pixels are required");
        }

        if (request.Width <= 0 || request.Height <= 0)
        {
            return BadRequest("Valid image dimensions are required");
        }

        try
        {
            var algorithm = Enum.Parse<PerceptualHashAlgorithm>(request.Algorithm ?? "PHash", ignoreCase: true);
            var hash = _hasher.ComputeImageHash(request.Pixels, request.Width, request.Height, algorithm);

            _logger.LogInformation(
                "[PerceptualHash] Computed {Algorithm} hash for {Width}x{Height} image",
                algorithm, request.Width, request.Height);

            return Ok(hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PerceptualHash] Failed to compute image hash");
            return StatusCode(500, new { error = "Failed to compute image perceptual hash" });
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

        try
        {
            if (!ulong.TryParse(request.HashA, System.Globalization.NumberStyles.HexNumber, null, out var hashA) ||
                !ulong.TryParse(request.HashB, System.Globalization.NumberStyles.HexNumber, null, out var hashB))
            {
                return BadRequest("Hash values must be valid hexadecimal numbers");
            }

            var distance = _hasher.HammingDistance(hashA, hashB);
            var similarity = _hasher.Similarity(hashA, hashB);
            var areSimilar = _hasher.AreSimilar(hashA, hashB, request.Threshold);

            return Ok(new
            {
                hashA = request.HashA,
                hashB = request.HashB,
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

