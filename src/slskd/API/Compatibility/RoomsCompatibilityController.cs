// <copyright file="RoomsCompatibilityController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.Compatibility;

using slskd.Core.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Provides slskd-compatible rooms API.
/// </summary>
[ApiController]
[Route("api/rooms")]
[Produces("application/json")]
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class RoomsCompatibilityController : ControllerBase
{
    private readonly ILogger<RoomsCompatibilityController> logger;

    public RoomsCompatibilityController(ILogger<RoomsCompatibilityController> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Join a room (slskd compatibility).
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> JoinRoom(
        [FromBody] JoinRoomRequest? request,
        CancellationToken cancellationToken = default)
    {
        var roomName = request?.Room?.Trim();
        if (string.IsNullOrWhiteSpace(roomName))
        {
            // Try to read from body if not bound
            if (Request.HasJsonContentType())
            {
                if (!Request.Body.CanSeek)
                {
                    Request.EnableBuffering();
                }

                Request.Body.Position = 0;
                using var reader = new System.IO.StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync(cancellationToken);
                Request.Body.Position = 0;
                if (!string.IsNullOrEmpty(body))
                {
                    try
                    {
                        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(body);
                        if (json.TryGetProperty("room", out var roomProp))
                            roomName = roomProp.GetString()?.Trim();
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        logger.LogDebug(ex, "Failed to parse RoomsCompatibility join room request payload");
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(roomName))
        {
            return BadRequest(new { error = "Room is required" });
        }

        logger.LogInformation("Join room requested: {Room}", roomName);

        await Task.CompletedTask;
        return Ok(new { room = roomName, joined = true });
    }

    /// <summary>
    /// Leave a room (slskd compatibility).
    /// </summary>
    [HttpDelete("{roomName}")]
    [Authorize]
    public async Task<IActionResult> LeaveRoom(
            string roomName,
            CancellationToken cancellationToken = default)
    {
        roomName = roomName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(roomName))
        {
            return BadRequest(new { error = "Room is required" });
        }

        logger.LogInformation("Leave room requested: {Room}", roomName);

        await Task.CompletedTask;
        return Ok(new { room = roomName, left = true });
    }
}

public record JoinRoomRequest(string? Room);
