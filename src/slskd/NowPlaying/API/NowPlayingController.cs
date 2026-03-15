// <copyright file="NowPlayingController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.NowPlaying.API;

using System.Collections.Generic;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Core.Security;

/// <summary>
///     Now Playing / Scrobble integration (#39).
///     Accepts webhook payloads from Plex, Jellyfin, Tautulli, and a generic JSON format.
/// </summary>
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("0")]
[ApiController]
[Produces("application/json")]
[ValidateCsrfForCookiesOnly]
public class NowPlayingController : ControllerBase
{
    public NowPlayingController(NowPlayingService nowPlaying)
    {
        NowPlaying = nowPlaying;
    }

    private NowPlayingService NowPlaying { get; }

    /// <summary>
    ///     Gets the currently playing track.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AuthPolicy.Any)]
    public IActionResult Get()
    {
        return Ok(NowPlaying.CurrentTrack);
    }

    /// <summary>
    ///     Sets the currently playing track (generic JSON).
    ///     Body: { "artist": "...", "title": "...", "album": "..." }
    /// </summary>
    [HttpPut]
    [Authorize(Policy = AuthPolicy.Any)]
    public IActionResult Put([FromBody] NowPlayingRequest request)
    {
        NowPlaying.SetTrack(request.Artist, request.Title, request.Album);
        return NoContent();
    }

    /// <summary>
    ///     Clears the currently playing track.
    /// </summary>
    [HttpDelete]
    [Authorize(Policy = AuthPolicy.Any)]
    public IActionResult Delete()
    {
        NowPlaying.Clear();
        return NoContent();
    }

    /// <summary>
    ///     Webhook receiver for Plex Media Server, Jellyfin, and Tautulli.
    ///     Plex sends multipart/form-data with a "payload" JSON field.
    ///     Jellyfin/Emby send application/json directly.
    ///     Tautulli can be configured to POST generic JSON.
    /// </summary>
    [HttpPost("webhook")]
    [Authorize(Policy = AuthPolicy.Any)]
    [Consumes("application/json", "multipart/form-data", "application/x-www-form-urlencoded")]
    public IActionResult Webhook()
    {
        string json = null;

        // Plex sends multipart/form-data with a "payload" field
        if (Request.HasFormContentType)
        {
            json = Request.Form["payload"].ToString();
        }
        else
        {
            using var reader = new System.IO.StreamReader(Request.Body);
            json = reader.ReadToEndAsync().GetAwaiter().GetResult();
        }

        if (string.IsNullOrWhiteSpace(json))
            return BadRequest("Empty payload");

        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Detect Plex: has "event" and "Metadata" fields
            if (root.TryGetProperty("event", out var plexEvent) &&
                root.TryGetProperty("Metadata", out var meta))
            {
                var ev = plexEvent.GetString();

                // Only handle play/resume events; clear on stop/pause
                if (ev is "media.play" or "media.resume" or "media.scrobble")
                {
                    var title = meta.TryGetProperty("title", out var t) ? t.GetString() : null;
                    var artist = meta.TryGetProperty("grandparentTitle", out var gp) ? gp.GetString()
                               : meta.TryGetProperty("originalTitle", out var ot) ? ot.GetString()
                               : null;
                    var album = meta.TryGetProperty("parentTitle", out var pt) ? pt.GetString() : null;

                    if (!string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(title))
                        NowPlaying.SetTrack(artist, title, album);
                }
                else if (ev is "media.pause" or "media.stop")
                {
                    NowPlaying.Clear();
                }

                return Ok();
            }

            // Detect Jellyfin/Emby: has "NotificationType" or "ItemType"
            if (root.TryGetProperty("NotificationType", out var jellyType))
            {
                var type = jellyType.GetString();
                if (type is "PlaybackStart" or "PlaybackProgress")
                {
                    var title = root.TryGetProperty("Name", out var n) ? n.GetString() : null;
                    var artist = root.TryGetProperty("Artist", out var a) ? a.GetString() : null;
                    var album = root.TryGetProperty("Album", out var alb) ? alb.GetString() : null;

                    if (!string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(title))
                        NowPlaying.SetTrack(artist, title, album);
                }
                else if (type is "PlaybackStop")
                {
                    NowPlaying.Clear();
                }

                return Ok();
            }

            // Generic fallback: { "artist": "...", "title": "...", "album": "...", "event": "play|stop" }
            {
                var evt = root.TryGetProperty("event", out var e) ? e.GetString() : "play";
                var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
                var artist = root.TryGetProperty("artist", out var a) ? a.GetString() : null;
                var album = root.TryGetProperty("album", out var alb) ? alb.GetString() : null;

                if (evt is "stop" or "pause")
                {
                    NowPlaying.Clear();
                }
                else if (!string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(title))
                {
                    NowPlaying.SetTrack(artist, title, album);
                }
            }

            return Ok();
        }
        catch (JsonException)
        {
            return BadRequest("Invalid JSON payload");
        }
    }
}

public record NowPlayingRequest
{
    public string Artist { get; init; }
    public string Title { get; init; }
    public string Album { get; init; }
}
