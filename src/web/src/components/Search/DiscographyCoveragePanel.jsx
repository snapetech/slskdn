import {
  fetchDiscographyCoverage,
  promoteDiscographyCoverageToWishlist,
} from '../../lib/musicBrainz';
import React, { useMemo, useState } from 'react';
import { toast } from 'react-toastify';
import {
  Button,
  Dropdown,
  Form,
  Header,
  Input,
  Label,
  List,
  Message,
  Popup,
  Progress,
  Segment,
} from 'semantic-ui-react';

const profileOptions = [
  { key: 'core', text: 'Core', value: 'CoreDiscography' },
  { key: 'extended', text: 'Extended', value: 'ExtendedDiscography' },
  { key: 'all', text: 'All releases', value: 'AllReleases' },
];

const statusColor = (status) => {
  switch (status) {
    case 'MeshAvailable':
      return 'teal';
    case 'WishlistSeeded':
      return 'blue';
    case 'Ambiguous':
      return 'orange';
    default:
      return 'grey';
  }
};

const statusText = (status) => {
  switch (status) {
    case 'MeshAvailable':
      return 'Available';
    case 'WishlistSeeded':
      return 'Wishlist';
    case 'Ambiguous':
      return 'Ambiguous';
    default:
      return 'Missing';
  }
};

const DiscographyCoveragePanel = ({ disabled }) => {
  const [artistId, setArtistId] = useState('');
  const [coverage, setCoverage] = useState(null);
  const [error, setError] = useState(null);
  const [filter, setFilter] = useState('flac');
  const [loading, setLoading] = useState(false);
  const [profile, setProfile] = useState('CoreDiscography');
  const [promoting, setPromoting] = useState(false);

  const missingCount = useMemo(() => {
    if (!coverage) {
      return 0;
    }

    return coverage.releases
      .flatMap((release) => release.tracks || [])
      .filter((track) => track.status === 'Absent').length;
  }, [coverage]);

  const loadCoverage = async ({ forceRefresh = false } = {}) => {
    const normalizedArtistId = artistId.trim();
    if (!normalizedArtistId) {
      toast.error('Artist MBID is required');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await fetchDiscographyCoverage({
        artistId: normalizedArtistId,
        forceRefresh,
        profile,
      });
      setCoverage(response.data);
    } catch (loadError) {
      console.error(loadError);
      setError(
        loadError?.response?.data ??
          loadError?.message ??
          'Unable to load discography coverage',
      );
    } finally {
      setLoading(false);
    }
  };

  const promoteMissing = async () => {
    if (!coverage?.artistId || missingCount === 0) {
      return;
    }

    setPromoting(true);

    try {
      const response = await promoteDiscographyCoverageToWishlist({
        artistId: coverage.artistId,
        filter: filter.trim() || 'flac',
        profile,
      });
      toast.success(
        `Added ${response.data?.createdCount ?? 0} missing tracks to Wishlist`,
      );
      await loadCoverage();
    } catch (promoteError) {
      console.error(promoteError);
      toast.error(
        promoteError?.response?.data ??
          promoteError?.message ??
          'Unable to add missing tracks to Wishlist',
      );
    } finally {
      setPromoting(false);
    }
  };

  if (disabled) {
    return (
      <Segment
        className="discography-coverage-segment"
        raised
      >
        <Header as="h4">Discography Concierge</Header>
        <p>Connect to the server to map artist coverage.</p>
      </Segment>
    );
  }

  return (
    <Segment
      className="discography-coverage-segment"
      loading={loading}
      raised
    >
      <Header as="h4">Discography Concierge</Header>
      <Form>
        <Form.Group widths="equal">
          <Form.Field>
            <Input
              disabled={loading}
              label="Artist MBID"
              onChange={(event) => setArtistId(event.target.value)}
              placeholder="e.g. 83d91898-..."
              value={artistId}
            />
          </Form.Field>
          <Form.Field>
            <Dropdown
              disabled={loading}
              fluid
              onChange={(event, data) => setProfile(data.value)}
              options={profileOptions}
              selection
              value={profile}
            />
          </Form.Field>
          <Form.Field>
            <Input
              disabled={loading}
              label="Wishlist filter"
              onChange={(event) => setFilter(event.target.value)}
              value={filter}
            />
          </Form.Field>
        </Form.Group>
        <Popup
          content="Build a per-release coverage map for this MusicBrainz artist using cached release data, verified HashDb evidence, and existing Wishlist seeds."
          position="top center"
          trigger={
            <Button
              disabled={loading}
              loading={loading}
              onClick={() => loadCoverage()}
              primary
            >
              Map coverage
            </Button>
          }
        />
        <Popup
          content="Refresh MusicBrainz release-group data before rebuilding the coverage map. This may contact MusicBrainz but does not browse Soulseek peers."
          position="top center"
          trigger={
            <Button
              disabled={loading}
              onClick={() => loadCoverage({ forceRefresh: true })}
              style={{ marginLeft: '0.5em' }}
            >
              Refresh catalog
            </Button>
          }
        />
        <Popup
          content="Create conservative Wishlist searches for currently missing tracks without starting immediate searches or downloads."
          position="top center"
          trigger={
            <Button
              disabled={!coverage || missingCount === 0 || promoting}
              loading={promoting}
              onClick={promoteMissing}
              style={{ marginLeft: '0.5em' }}
            >
              Add missing to Wishlist
            </Button>
          }
        />
      </Form>
      {error && (
        <Message
          content={error}
          negative
        />
      )}
      {coverage && (
        <div className="discography-coverage-results">
          <div className="discography-coverage-summary">
            <Header as="h5">{coverage.artistName || coverage.artistId}</Header>
            <span>
              {coverage.coveredTracks}/{coverage.totalTracks} tracks covered
            </span>
            {coverage.promotionSuggested && (
              <Label
                color="violet"
                size="mini"
              >
                completion mission
              </Label>
            )}
          </div>
          <Progress
            percent={Math.round((coverage.coverageRatio || 0) * 100)}
            progress
            size="tiny"
          />
          <div className="discography-release-list">
            {(coverage.releases || []).map((release) => (
              <Segment
                className="discography-release-card"
                key={release.releaseId}
                secondary
              >
                <div className="discography-release-head">
                  <Header as="h5">{release.title || release.releaseId}</Header>
                  <span>
                    {release.coveredTracks}/{release.totalTracks} tracks
                  </span>
                </div>
                <List
                  divided
                  relaxed
                >
                  {(release.tracks || []).map((track) => (
                    <List.Item
                      key={`${release.releaseId}-${track.position}-${track.recordingId || track.title}`}
                    >
                      <List.Content>
                        <List.Header>
                          {track.position}. {track.title}
                          <Label
                            color={statusColor(track.status)}
                            size="tiny"
                            style={{ marginLeft: '0.5em' }}
                          >
                            {statusText(track.status)}
                          </Label>
                        </List.Header>
                        <List.Description>
                          {track.artist || coverage.artistName} ·{' '}
                          {track.recordingId || 'unknown recording'}
                          {track.matches?.length > 0 &&
                            ` · ${track.matches.length} verified match${track.matches.length === 1 ? '' : 'es'}`}
                        </List.Description>
                      </List.Content>
                    </List.Item>
                  ))}
                </List>
              </Segment>
            ))}
          </div>
        </div>
      )}
    </Segment>
  );
};

export default DiscographyCoveragePanel;
