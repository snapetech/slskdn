// <copyright file="BrainzClient.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.Brainz;

using System.Collections.Concurrent;
using slskd.Integrations.AcoustId;
using slskd.Integrations.MusicBrainz;
using slskd.Integrations.MusicBrainz.Models;

public interface IBrainzClient
{
    Task<TrackTarget?> GetRecordingAsync(string recordingId, CancellationToken cancellationToken = default);

    Task<AlbumTarget?> GetReleaseAsync(string releaseId, CancellationToken cancellationToken = default);

    Task<AlbumTarget?> GetReleaseByDiscogsReleaseIdAsync(string discogsReleaseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecordingSearchHit>> SearchRecordingsAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default);

    Task<BrainzFingerprintLookupResult?> LookupFingerprintAsync(
        string fingerprint,
        int sampleRate,
        int durationSeconds,
        CancellationToken cancellationToken = default);
}

public sealed class BrainzClient : IBrainzClient
{
    private readonly IAcoustIdClient _acoustIdClient;
    private readonly IMusicBrainzClient _musicBrainzClient;
    private readonly ConcurrentDictionary<string, Task<TrackTarget?>> _recordingCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<AlbumTarget?>> _releaseCache = new(StringComparer.OrdinalIgnoreCase);

    public BrainzClient(IMusicBrainzClient musicBrainzClient, IAcoustIdClient acoustIdClient)
    {
        _musicBrainzClient = musicBrainzClient;
        _acoustIdClient = acoustIdClient;
    }

    public Task<TrackTarget?> GetRecordingAsync(string recordingId, CancellationToken cancellationToken = default)
    {
        var normalizedId = NormalizeIdentifier(recordingId);
        if (string.IsNullOrEmpty(normalizedId))
        {
            return Task.FromResult<TrackTarget?>(null);
        }

        return GetCachedAsync(
            _recordingCache,
            normalizedId,
            key => _musicBrainzClient.GetRecordingAsync(key, cancellationToken));
    }

    public Task<AlbumTarget?> GetReleaseAsync(string releaseId, CancellationToken cancellationToken = default)
    {
        var normalizedId = NormalizeIdentifier(releaseId);
        if (string.IsNullOrEmpty(normalizedId))
        {
            return Task.FromResult<AlbumTarget?>(null);
        }

        return GetCachedAsync(
            _releaseCache,
            normalizedId,
            key => _musicBrainzClient.GetReleaseAsync(key, cancellationToken));
    }

    public Task<AlbumTarget?> GetReleaseByDiscogsReleaseIdAsync(
        string discogsReleaseId,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = NormalizeIdentifier(discogsReleaseId);
        return string.IsNullOrEmpty(normalizedId)
            ? Task.FromResult<AlbumTarget?>(null)
            : _musicBrainzClient.GetReleaseByDiscogsReleaseIdAsync(normalizedId, cancellationToken);
    }

    public async Task<IReadOnlyList<RecordingSearchHit>> SearchRecordingsAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim();
        if (string.IsNullOrEmpty(normalizedQuery) || limit <= 0)
        {
            return Array.Empty<RecordingSearchHit>();
        }

        var results = await _musicBrainzClient.SearchRecordingsAsync(
            normalizedQuery,
            Math.Min(limit, 50),
            cancellationToken).ConfigureAwait(false);

        return results
            .Where(hit => !string.IsNullOrWhiteSpace(hit.RecordingId))
            .GroupBy(hit => hit.RecordingId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => NormalizeSearchHit(group.First()))
            .Take(limit)
            .ToArray();
    }

    public async Task<BrainzFingerprintLookupResult?> LookupFingerprintAsync(
        string fingerprint,
        int sampleRate,
        int durationSeconds,
        CancellationToken cancellationToken = default)
    {
        var normalizedFingerprint = fingerprint.Trim();
        if (string.IsNullOrEmpty(normalizedFingerprint) || sampleRate <= 0 || durationSeconds <= 0)
        {
            return null;
        }

        var acoustIdResult = await _acoustIdClient.LookupAsync(
            normalizedFingerprint,
            sampleRate,
            durationSeconds,
            cancellationToken).ConfigureAwait(false);

        if (acoustIdResult?.Recordings is not { Length: > 0 })
        {
            return null;
        }

        var bestRecording = acoustIdResult.Recordings
            .Where(recording => !string.IsNullOrWhiteSpace(recording.Id))
            .OrderByDescending(recording => HasUsefulTitle(recording.Title))
            .ThenBy(recording => recording.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (bestRecording is null)
        {
            return null;
        }

        var recordingId = bestRecording.Id.Trim();
        var musicBrainzRecording = await GetRecordingAsync(recordingId, cancellationToken).ConfigureAwait(false);

        return new BrainzFingerprintLookupResult
        {
            AcoustId = acoustIdResult.Id.Trim(),
            Score = acoustIdResult.Score,
            RecordingId = recordingId,
            Title = FirstNonEmpty(musicBrainzRecording?.Title, bestRecording.Title),
            Artist = FirstNonEmpty(musicBrainzRecording?.Artist, string.Join(
                " & ",
                bestRecording.Artists
                    .Select(artist => artist.Name.Trim())
                    .Where(name => !string.IsNullOrEmpty(name)))),
            MusicBrainzRecording = musicBrainzRecording,
        };
    }

    private static RecordingSearchHit NormalizeSearchHit(RecordingSearchHit hit)
    {
        return new RecordingSearchHit(
            hit.RecordingId.Trim(),
            hit.Title.Trim(),
            hit.Artist.Trim(),
            string.IsNullOrWhiteSpace(hit.MusicBrainzArtistId) ? null : hit.MusicBrainzArtistId.Trim());
    }

    private static async Task<T?> GetCachedAsync<T>(
        ConcurrentDictionary<string, Task<T?>> cache,
        string key,
        Func<string, Task<T?>> factory)
        where T : class
    {
        var task = cache.GetOrAdd(key, factory);
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch
        {
            cache.TryRemove(key, out _);
            throw;
        }
    }

    private static string NormalizeIdentifier(string value)
    {
        return value.Trim();
    }

    private static bool HasUsefulTitle(string value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.Select(value => value?.Trim()).FirstOrDefault(value => !string.IsNullOrEmpty(value)) ?? string.Empty;
    }
}

public sealed record BrainzFingerprintLookupResult
{
    public string AcoustId { get; init; } = string.Empty;

    public double Score { get; init; }

    public string RecordingId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Artist { get; init; } = string.Empty;

    public TrackTarget? MusicBrainzRecording { get; init; }
}
