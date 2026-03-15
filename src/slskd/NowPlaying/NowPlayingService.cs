// <copyright file="NowPlayingService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.NowPlaying;

using System;
using Serilog;

/// <summary>
///     Holds the currently playing track and exposes it for use in user info (#39).
/// </summary>
public class NowPlayingService
{
    private static readonly ILogger Log = Serilog.Log.ForContext<NowPlayingService>();

    /// <summary>
    ///     Gets the current track, or null when nothing is playing.
    /// </summary>
    public NowPlayingTrack CurrentTrack { get; private set; }

    /// <summary>
    ///     Sets the currently playing track.
    /// </summary>
    public void SetTrack(string artist, string title, string album = null)
    {
        if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title))
        {
            Log.Warning("NowPlaying: received empty artist or title; ignoring");
            return;
        }

        CurrentTrack = new NowPlayingTrack
        {
            Artist = artist.Trim(),
            Title = title.Trim(),
            Album = album?.Trim(),
            StartedAt = DateTimeOffset.UtcNow,
        };

        Log.Information("NowPlaying: {Artist} – {Title}", CurrentTrack.Artist, CurrentTrack.Title);
    }

    /// <summary>
    ///     Clears the currently playing track (e.g. on pause/stop).
    /// </summary>
    public void Clear()
    {
        CurrentTrack = null;
        Log.Information("NowPlaying: cleared");
    }

    /// <summary>
    ///     Returns the now-playing suffix to append to the user description, or null when nothing is playing.
    /// </summary>
    public string GetDescriptionSuffix()
    {
        var track = CurrentTrack;
        if (track == null)
            return null;

        return $"\n\n\uD83C\uDFB5 Listening to: {track.Artist} \u2013 {track.Title}";
    }
}

/// <summary>
///     Represents a currently playing track.
/// </summary>
public record NowPlayingTrack
{
    public string Artist { get; init; }
    public string Title { get; init; }
    public string Album { get; init; }
    public DateTimeOffset StartedAt { get; init; }
}
