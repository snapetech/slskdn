// <copyright file="RoomsCompatibilityController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.Compatibility;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Provides slskd-compatible rooms API.
/// </summary>
[ApiController]
[Route("api/rooms")]
[Produces("application/json")]
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
        var roomName = request?.Room;
        if (string.IsNullOrWhiteSpace(roomName))
        {
            // Try to read from body if not bound
            if (Request.HasJsonContentType() && Request.Body.CanSeek)
            {
                Request.Body.Position = 0;
                using var reader = new System.IO.StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();
                if (!string.IsNullOrEmpty(body))
                {
                    try
                    {
                        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(body);
                        if (json.TryGetProperty("room", out var roomProp))
                            roomName = roomProp.GetString();
                    }
                    catch { }
                }
            }
        }
        
        roomName ??= "default";
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
        logger.LogInformation("Leave room requested: {Room}", roomName);

        await Task.CompletedTask;
        return Ok(new { room = roomName, left = true });
    }
}

public record JoinRoomRequest(string? Room);
