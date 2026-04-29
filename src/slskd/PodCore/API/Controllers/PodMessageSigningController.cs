// <copyright file="PodMessageSigningController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.PodCore.API.Controllers;

using slskd.Core.Security;

/// <summary>
/// Pod message signing API controller.
/// </summary>
[Route("api/v0/podcore/signing")]
[ApiController]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class PodMessageSigningController : ControllerBase
{
    private readonly ILogger<PodMessageSigningController> _logger;
    private readonly IMessageSigner _messageSigner;

    public PodMessageSigningController(
        ILogger<PodMessageSigningController> logger,
        IMessageSigner messageSigner)
    {
        _logger = logger;
        _messageSigner = messageSigner;
    }

    /// <summary>
    /// Signs a pod message.
    /// </summary>
    /// <param name="request">The signing request with message and private key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The signed message.</returns>
    [HttpPost("sign")]
    public async Task<IActionResult> SignMessage([FromBody] MessageSigningRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Message == null)
        {
            return BadRequest(new { error = "Valid message and private key are required" });
        }

        var normalizedRequest = request with
        {
            PrivateKey = request.PrivateKey?.Trim() ?? string.Empty,
            Message = NormalizeMessage(request.Message),
        };

        if (string.IsNullOrWhiteSpace(normalizedRequest.PrivateKey))
        {
            return BadRequest(new { error = "Valid message and private key are required" });
        }

        try
        {
            var signedMessage = await _messageSigner.SignMessageAsync(normalizedRequest.Message, normalizedRequest.PrivateKey, cancellationToken);

            return Ok(signedMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMessageSigning] Error signing message");
            return StatusCode(500, new { error = "Failed to sign message" });
        }
    }

    /// <summary>
    /// Verifies a pod message signature.
    /// </summary>
    /// <param name="message">The message to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verification result.</returns>
    [HttpPost("verify")]
    public async Task<IActionResult> VerifyMessage([FromBody] PodMessage message, CancellationToken cancellationToken = default)
    {
        var normalizedMessage = message == null ? null : NormalizeMessage(message);
        if (normalizedMessage == null || string.IsNullOrWhiteSpace(normalizedMessage.MessageId))
        {
            return BadRequest(new { error = "Valid message is required" });
        }

        try
        {
            var isValid = await _messageSigner.VerifyMessageAsync(normalizedMessage, cancellationToken);

            return Ok(new { isValid });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMessageSigning] Error verifying message {MessageId}", normalizedMessage?.MessageId);
            return StatusCode(500, new { error = "Failed to verify message" });
        }
    }

    /// <summary>
    /// Generates a new key pair for message signing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated key pair.</returns>
    [HttpPost("generate-keypair")]
    public async Task<IActionResult> GenerateKeyPair(CancellationToken cancellationToken = default)
    {
        try
        {
            var keyPair = await _messageSigner.GenerateKeyPairAsync(cancellationToken);

            return Ok(keyPair);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMessageSigning] Error generating key pair");
            return StatusCode(500, new { error = "Failed to generate key pair" });
        }
    }

    /// <summary>
    /// Gets message signing statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Signing statistics.</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetSigningStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _messageSigner.GetStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMessageSigning] Error getting signing stats");
            return StatusCode(500, new { error = "Failed to get signing statistics" });
        }
    }

    private static PodMessage NormalizeMessage(PodMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new PodMessage
        {
            MessageId = message.MessageId?.Trim() ?? string.Empty,
            PodId = message.PodId?.Trim() ?? string.Empty,
            ChannelId = message.ChannelId?.Trim() ?? string.Empty,
            SenderPeerId = message.SenderPeerId?.Trim() ?? string.Empty,
            Body = message.Body?.Trim() ?? string.Empty,
            TimestampUnixMs = message.TimestampUnixMs,
            Signature = message.Signature?.Trim() ?? string.Empty,
            SigVersion = message.SigVersion,
        };
    }
}

/// <summary>
/// Request to sign a message.
/// </summary>
public record MessageSigningRequest(
    PodMessage Message,
    string PrivateKey);
