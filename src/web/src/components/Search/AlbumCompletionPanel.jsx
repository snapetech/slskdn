import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Button, Header, Label, List, Message, Segment } from 'semantic-ui-react';
import { fetchAlbumCompletion } from '../../lib/musicBrainz';

const formatDuration = (durationMs) => {
  if (!durationMs) {
    return 'Unknown length';
  }

  const minutes = Math.floor(durationMs / 60000);
  const seconds = Math.round((durationMs % 60000) / 1000);
  return `${minutes}:${seconds.toString().padStart(2, '0')} min`;
};

const AlbumCompletionPanel = ({ disabled }) => {
  const [albums, setAlbums] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const loadAlbums = useCallback(async () => {
    if (disabled) {
      setAlbums([]);
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await fetchAlbumCompletion();
      setAlbums(response.data?.albums ?? []);
    } catch (loadError) {
      console.error(loadError);
      setError(
        loadError?.response?.data ?? loadError?.message ?? 'Unable to load album completion data',
      );
    } finally {
      setLoading(false);
    }
  }, [disabled]);

  useEffect(() => {
    loadAlbums();
  }, [loadAlbums]);

  const albumCount = albums.length;

  const displayAlbums = useMemo(() => albums, [albums]);

  if (disabled) {
    return (
      <Segment raised className="album-completion-segment">
        <Header as="h4">Album completion status</Header>
        <p>Connect to the server to view album targets and track progress.</p>
      </Segment>
    );
  }

  return (
    <Segment raised className="album-completion-segment" loading={loading}>
      <Header as="h4">
        Album completion status
        <Button
          floated="right"
          size="mini"
          onClick={loadAlbums}
          disabled={loading}
        >
          Refresh
        </Button>
      </Header>
      {error && <Message negative content={error} />}
      {!albumCount && !loading && !error && (
        <p>No album targets yet. Resolve a MusicBrainz release or recording to start tracking completion.</p>
      )}
      <div className="album-completion-list">
        {displayAlbums.map((album) => {
          const missingTracks = album.tracks.filter((track) => !track.complete);
          const ratio = album.totalTracks > 0 ? `${album.completedTracks}/${album.totalTracks}` : '0/0';
          return (
            <Segment key={album.releaseId} className="album-summary-segment">
              <div className="album-summary-head">
                <Header as="h5" className="album-completion-title">
                  {album.title}
                  {album.discogsReleaseId && (
                    <Label size="mini" className="album-track-label" color="orange">
                      Discogs #{album.discogsReleaseId}
                    </Label>
                  )}
                </Header>
                <div className="album-completion-meta">
                  <span>{album.artist}</span>
                  <span>{album.releaseDate ?? 'Unknown date'}</span>
                  <span>{ratio} tracks complete</span>
                </div>
              </div>
              {missingTracks.length > 0 ? (
                <div className="album-missing-tracks">
                  <Header as="h6" className="album-missing-header">
                    Missing tracks
                  </Header>
                  <List divided relaxed>
                    {missingTracks.map((track) => (
                      <List.Item
                        key={`${album.releaseId}-${track.position}-${track.title}`}
                        className="album-track-row"
                      >
                        <List.Content>
                          <List.Header>
                            {track.position}. {track.title}
                            {track.matches?.length > 0 && (
                              <Label size="tiny" color="blue" className="album-track-label">
                                {track.matches.length} fingerprint match{track.matches.length === 1 ? '' : 'es'}
                              </Label>
                            )}
                          </List.Header>
                          <List.Description>
                            Recording ID: {track.recordingId ?? 'unknown'} · {formatDuration(track.durationMs)}
                          </List.Description>
                        </List.Content>
                      </List.Item>
                    ))}
                  </List>
                </div>
              ) : (
                <Label color="teal" size="mini">
                  Album complete
                </Label>
              )}
            </Segment>
          );
        })}
      </div>
    </Segment>
  );
};

export default AlbumCompletionPanel;
