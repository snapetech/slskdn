// <copyright file="PodMessageSigningController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.PodCore.API.Controllers;

using slskd.Core.Security;

/// <summary>
/// Pod message signing API controller.
/// </summary>
[Route("api/v0/podcore/signing")]
[ApiController]
[AllowAnonymous] // PR-02: intended-public
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
        if (request?.Message == null || string.IsNullOrWhiteSpace(request.PrivateKey))
        {
            return BadRequest(new { error = "Valid message and private key are required" });
        }

        try
        {
            var signedMessage = await _messageSigner.SignMessageAsync(request.Message, request.PrivateKey, cancellationToken);

            _logger.LogInformation("[PodMessageSigning] Signed message {MessageId}", signedMessage.MessageId);

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
        if (message == null || string.IsNullOrWhiteSpace(message.MessageId))
        {
            return BadRequest(new { error = "Valid message is required" });
        }

        try
        {
            var isValid = await _messageSigner.VerifyMessageAsync(message, cancellationToken);

            _logger.LogInformation("[PodMessageSigning] Verified message {MessageId}: {IsValid}", message.MessageId, isValid);

            return Ok(new { messageId = message.MessageId, isValid });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMessageSigning] Error verifying message {MessageId}", message?.MessageId);
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

            _logger.LogInformation("[PodMessageSigning] Generated new key pair");

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
}

/// <summary>
/// Request to sign a message.
/// </summary>
public record MessageSigningRequest(
    PodMessage Message,
    string PrivateKey);

