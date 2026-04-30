import * as lidarr from '../../../lib/lidarr';
import * as optionsApi from '../../../lib/options';
import {
  buildMediaServerPathDiagnostic,
  mediaServerAdapters,
} from '../../../lib/mediaServerIntegrations';
import {
  buildServarrReadiness,
  summarizeServarrReadiness,
} from '../../../lib/servarrReadiness';
import React, { useEffect, useMemo, useState } from 'react';
import {
  Button,
  Card,
  Checkbox,
  Form,
  Header,
  Icon,
  Input,
  Label,
  Message,
  Popup,
  Segment,
  Table,
} from 'semantic-ui-react';

const getOption = (source, ...keys) => {
  for (const key of keys) {
    if (source && Object.prototype.hasOwnProperty.call(source, key)) {
      return source[key];
    }
  }

  return undefined;
};

const getIntegrationsOptions = (options = {}) =>
  getOption(options, 'integration', 'Integration', 'integrations', 'Integrations') ||
  {};

const getVpnOptions = (options = {}) =>
  getOption(getIntegrationsOptions(options), 'vpn', 'Vpn', 'VPN') || {};

const getLidarrOptions = (options = {}) =>
  getOption(getIntegrationsOptions(options), 'lidarr', 'Lidarr') || {};

const getSpotifyOptions = (options = {}) =>
  getOption(getIntegrationsOptions(options), 'spotify', 'Spotify') || {};

const getYouTubeOptions = (options = {}) =>
  getOption(getIntegrationsOptions(options), 'youtube', 'YouTube') || {};

const getLastFmOptions = (options = {}) =>
  getOption(getIntegrationsOptions(options), 'lastfm', 'lastFm', 'LastFm') || {};

const getVpnState = (state = {}) => getOption(state, 'vpn', 'Vpn', 'VPN') || {};

const boolLabel = (value, trueText = 'Enabled', falseText = 'Disabled') => (
  <Label color={value ? 'green' : 'grey'}>
    <Icon name={value ? 'check circle' : 'minus circle'} />
    {value ? trueText : falseText}
  </Label>
);

const valueOrDash = (value) =>
  value === undefined || value === null || value === '' ? '-' : value;

const isConfigured = (value) =>
  value !== undefined && value !== null && value !== '';

const toNumber = (value, fallback) => {
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) ? parsed : fallback;
};

const portForwards = (vpn = {}) =>
  getOption(vpn, 'portForwards', 'PortForwards') || [];

const buildSourceFeedForm = (options = {}) => {
  const spotify = getSpotifyOptions(options);
  const youtube = getYouTubeOptions(options);
  const lastfm = getLastFmOptions(options);

  return {
    lastFmApiKey: '',
    lastFmConfigured: isConfigured(getOption(lastfm, 'apiKey', 'ApiKey')),
    lastFmEnabled: Boolean(getOption(lastfm, 'enabled', 'Enabled')),
    spotifyClientId: '',
    spotifyClientSecret: '',
    spotifyConfigured: isConfigured(getOption(spotify, 'clientId', 'ClientId')),
    spotifyEnabled: Boolean(getOption(spotify, 'enabled', 'Enabled')),
    spotifyMaxItems: String(
      getOption(spotify, 'maxItemsPerImport', 'MaxItemsPerImport') ?? 500,
    ),
    spotifyMarket: getOption(spotify, 'market', 'Market') || 'US',
    spotifyRedirectUri: getOption(spotify, 'redirectUri', 'RedirectUri') || '',
    spotifySecretConfigured: isConfigured(
      getOption(spotify, 'clientSecret', 'ClientSecret'),
    ),
    spotifyTimeout: String(getOption(spotify, 'timeoutSeconds', 'TimeoutSeconds') ?? 20),
    youTubeApiKey: '',
    youTubeConfigured: isConfigured(getOption(youtube, 'apiKey', 'ApiKey')),
    youTubeEnabled: Boolean(getOption(youtube, 'enabled', 'Enabled')),
  };
};

const SourceFeedIntegrationsPanel = ({ options }) => {
  const remoteConfiguration = Boolean(
    getOption(options, 'remoteConfiguration', 'RemoteConfiguration'),
  );
  const [form, setForm] = useState(() => buildSourceFeedForm(options));
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState(null);

  useEffect(() => {
    setForm(buildSourceFeedForm(options));
  }, [options]);

  const update = (key, value) => {
    setForm((current) => ({ ...current, [key]: value }));
  };

  const reset = () => {
    setForm(buildSourceFeedForm(options));
    setMessage(null);
  };

  const missingRequiredSettings = [
    form.spotifyEnabled &&
      !form.spotifyConfigured &&
      !form.spotifyClientId.trim() &&
      'Spotify needs a client ID before account connection or provider imports can run.',
    form.youTubeEnabled &&
      !form.youTubeConfigured &&
      !form.youTubeApiKey.trim() &&
      'YouTube needs a Data API key before playlist expansion can run.',
    form.lastFmEnabled &&
      !form.lastFmConfigured &&
      !form.lastFmApiKey.trim() &&
      'Last.fm needs an API key before loved/recent/top imports can run.',
  ].filter(Boolean);

  const save = async () => {
    setSaving(true);
    setMessage(null);

    const spotifyPatch = {
      enabled: form.spotifyEnabled,
      maxItemsPerImport: toNumber(form.spotifyMaxItems, 500),
      market: form.spotifyMarket.trim().toUpperCase(),
      redirectUri: form.spotifyRedirectUri.trim(),
      timeoutSeconds: toNumber(form.spotifyTimeout, 20),
    };

    if (form.spotifyClientId.trim()) {
      spotifyPatch.clientId = form.spotifyClientId.trim();
    }

    if (form.spotifyClientSecret.trim()) {
      spotifyPatch.clientSecret = form.spotifyClientSecret.trim();
    }

    const youtubePatch = {
      enabled: form.youTubeEnabled,
    };

    if (form.youTubeApiKey.trim()) {
      youtubePatch.apiKey = form.youTubeApiKey.trim();
    }

    const lastFmPatch = {
      enabled: form.lastFmEnabled,
    };

    if (form.lastFmApiKey.trim()) {
      lastFmPatch.apiKey = form.lastFmApiKey.trim();
    }

    try {
      await optionsApi.applyOverlay({
        integration: {
          lastFm: lastFmPatch,
          spotify: spotifyPatch,
          youTube: youtubePatch,
        },
      });
      setForm((current) => ({
        ...current,
        lastFmApiKey: '',
        lastFmConfigured: current.lastFmConfigured || Boolean(lastFmPatch.apiKey),
        spotifyClientId: '',
        spotifyClientSecret: '',
        spotifyConfigured: current.spotifyConfigured || Boolean(spotifyPatch.clientId),
        spotifySecretConfigured:
          current.spotifySecretConfigured || Boolean(spotifyPatch.clientSecret),
        youTubeApiKey: '',
        youTubeConfigured: current.youTubeConfigured || Boolean(youtubePatch.apiKey),
      }));
      setMessage({
        positive: true,
        text: 'Source-feed integration settings applied for this running daemon.',
      });
    } catch (error) {
      setMessage({
        negative: true,
        text:
          error?.response?.data ||
          error?.response?.statusText ||
          error?.message ||
          'Failed to apply source-feed integration settings.',
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card fluid>
      <Card.Content>
        <Card.Header>
          <Icon name="rss" />
          Source Feed Imports
        </Card.Header>
        <Card.Meta>
          Provider settings for Wishlist Import Feed previews.
        </Card.Meta>
      </Card.Content>
      <Card.Content>
        <div className="integration-status-row">
          {boolLabel(form.spotifyEnabled, 'Spotify On', 'Spotify Off')}
          <Label>
            <Icon name={form.spotifyConfigured ? 'key' : 'warning sign'} />
            Spotify Client ID {form.spotifyConfigured ? 'Configured' : 'Missing'}
          </Label>
          {boolLabel(form.youTubeEnabled, 'YouTube On', 'YouTube Off')}
          <Label>
            <Icon name={form.youTubeConfigured ? 'key' : 'warning sign'} />
            YouTube API Key {form.youTubeConfigured ? 'Configured' : 'Missing'}
          </Label>
          {boolLabel(form.lastFmEnabled, 'Last.fm On', 'Last.fm Off')}
          <Label>
            <Icon name={form.lastFmConfigured ? 'key' : 'warning sign'} />
            Last.fm API Key {form.lastFmConfigured ? 'Configured' : 'Missing'}
          </Label>
        </div>

        {!remoteConfiguration && (
          <Message
            info
            size="small"
          >
            Runtime configuration changes are disabled. Enable remote
            configuration or edit YAML in the Options tab to change these
            provider settings.
          </Message>
        )}

        {message && (
          <Message
            negative={message.negative}
            positive={message.positive}
            size="small"
          >
            {message.text}
          </Message>
        )}
        {missingRequiredSettings.length > 0 && (
          <Message
            size="small"
            warning
          >
            <Message.List items={missingRequiredSettings} />
          </Message>
        )}

        <Form className="source-feed-settings-form">
          <Segment>
            <Header as="h4">
              <Icon name="spotify" />
              Spotify
            </Header>
            <Popup
              content="Turns on Spotify source-feed imports and account connection. Private liked/saved/followed feeds still require a connected Spotify account or bearer token."
              trigger={
                <Checkbox
                  aria-label="Enable Spotify source-feed imports"
                  checked={form.spotifyEnabled}
                  disabled={!remoteConfiguration || saving}
                  label="Enable Spotify imports"
                  onChange={(_, { checked }) => update('spotifyEnabled', checked)}
                  toggle
                />
              }
            />
            <Form.Group widths="equal">
              <Form.Input
                aria-label="Spotify client ID"
                disabled={!remoteConfiguration || saving}
                label="Client ID"
                onChange={(_, { value }) => update('spotifyClientId', value)}
                placeholder={form.spotifyConfigured ? 'Configured' : 'Spotify app client ID'}
                type="password"
                value={form.spotifyClientId}
              />
              <Form.Input
                aria-label="Spotify client secret"
                disabled={!remoteConfiguration || saving}
                label="Client Secret"
                onChange={(_, { value }) => update('spotifyClientSecret', value)}
                placeholder={
                  form.spotifySecretConfigured
                    ? 'Configured'
                    : 'Optional for OAuth; required for app-token public imports'
                }
                type="password"
                value={form.spotifyClientSecret}
              />
            </Form.Group>
            <Form.Group widths="equal">
              <Form.Input
                aria-label="Spotify redirect URI"
                disabled={!remoteConfiguration || saving}
                label="Redirect URI"
                onChange={(_, { value }) => update('spotifyRedirectUri', value)}
                placeholder="Infer from current host"
                value={form.spotifyRedirectUri}
              />
              <Form.Input
                aria-label="Spotify market"
                disabled={!remoteConfiguration || saving}
                label="Market"
                maxLength={2}
                onChange={(_, { value }) => update('spotifyMarket', value)}
                value={form.spotifyMarket}
              />
            </Form.Group>
            <Form.Group widths="equal">
              <Form.Input
                aria-label="Spotify timeout seconds"
                disabled={!remoteConfiguration || saving}
                label="Timeout Seconds"
                min={1}
                onChange={(_, { value }) => update('spotifyTimeout', value)}
                type="number"
                value={form.spotifyTimeout}
              />
              <Form.Input
                aria-label="Spotify max items per import"
                disabled={!remoteConfiguration || saving}
                label="Max Items Per Import"
                min={1}
                onChange={(_, { value }) => update('spotifyMaxItems', value)}
                type="number"
                value={form.spotifyMaxItems}
              />
            </Form.Group>
          </Segment>

          <Segment>
            <Header as="h4">
              <Icon name="youtube play" />
              YouTube
            </Header>
            <Popup
              content="Turns on YouTube Data API playlist expansion for explicitly previewed Import Feed URLs."
              trigger={
                <Checkbox
                  aria-label="Enable YouTube playlist source-feed imports"
                  checked={form.youTubeEnabled}
                  disabled={!remoteConfiguration || saving}
                  label="Enable YouTube playlist expansion"
                  onChange={(_, { checked }) => update('youTubeEnabled', checked)}
                  toggle
                />
              }
            />
            <Form.Input
              aria-label="YouTube Data API key"
              disabled={!remoteConfiguration || saving}
              label="API Key"
              onChange={(_, { value }) => update('youTubeApiKey', value)}
              placeholder={form.youTubeConfigured ? 'Configured' : 'YouTube Data API key'}
              type="password"
              value={form.youTubeApiKey}
            />
          </Segment>

          <Segment>
            <Header as="h4">
              <Icon name="lastfm" />
              Last.fm
            </Header>
            <Popup
              content="Turns on Last.fm API imports for explicitly previewed loved, recent, and top-track user URLs."
              trigger={
                <Checkbox
                  aria-label="Enable Last.fm source-feed imports"
                  checked={form.lastFmEnabled}
                  disabled={!remoteConfiguration || saving}
                  label="Enable Last.fm imports"
                  onChange={(_, { checked }) => update('lastFmEnabled', checked)}
                  toggle
                />
              }
            />
            <Form.Input
              aria-label="Last.fm API key"
              disabled={!remoteConfiguration || saving}
              label="API Key"
              onChange={(_, { value }) => update('lastFmApiKey', value)}
              placeholder={form.lastFmConfigured ? 'Configured' : 'Last.fm API key'}
              type="password"
              value={form.lastFmApiKey}
            />
          </Segment>
        </Form>

        <div className="integration-actions">
          <Popup
            content="Apply these source-feed integration settings through the runtime configuration overlay."
            trigger={
              <Button
                disabled={!remoteConfiguration || missingRequiredSettings.length > 0}
                icon
                labelPosition="left"
                loading={saving}
                onClick={save}
                primary
              >
                <Icon name="save" />
                Apply Settings
              </Button>
            }
          />
          <Popup
            content="Discard unsaved edits and restore the values currently reported by the daemon."
            trigger={
              <Button
                disabled={saving}
                icon
                labelPosition="left"
                onClick={reset}
              >
                <Icon name="undo" />
                Reset
              </Button>
            }
          />
        </div>
      </Card.Content>
    </Card>
  );
};

const VpnPanel = ({ options, state }) => {
  const vpnOptions = getVpnOptions(options);
  const vpnState = getVpnState(state);
  const gluetun = getOption(vpnOptions, 'gluetun', 'Gluetun') || {};
  const forwards = portForwards(vpnState);

  return (
    <Card fluid>
      <Card.Content>
        <Card.Header>
          <Icon name="shield alternate" />
          VPN
        </Card.Header>
        <Card.Meta>Daemon VPN readiness and configured provider settings.</Card.Meta>
      </Card.Content>
      <Card.Content>
        <div className="integration-status-row">
          {boolLabel(getOption(vpnOptions, 'enabled', 'Enabled'))}
          {boolLabel(
            getOption(vpnState, 'isReady', 'IsReady'),
            'Ready',
            'Not Ready',
          )}
          {boolLabel(
            getOption(vpnState, 'isConnected', 'IsConnected'),
            'Connected',
            'Disconnected',
          )}
          {boolLabel(
            getOption(vpnOptions, 'portForwarding', 'PortForwarding'),
            'Port Forwarding',
            'No Port Forwarding',
          )}
        </div>
        <Table
          basic="very"
          compact
          definition
        >
          <Table.Body>
            <Table.Row>
              <Table.Cell>Provider</Table.Cell>
              <Table.Cell>
                {getOption(gluetun, 'url', 'Url') ? 'Gluetun' : '-'}
              </Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Control URL</Table.Cell>
              <Table.Cell>{valueOrDash(getOption(gluetun, 'url', 'Url'))}</Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Polling Interval</Table.Cell>
              <Table.Cell>
                {valueOrDash(getOption(vpnOptions, 'pollingInterval', 'PollingInterval'))}
                {' ms'}
              </Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Public IP</Table.Cell>
              <Table.Cell>
                {valueOrDash(getOption(vpnState, 'publicIPAddress', 'PublicIPAddress'))}
              </Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Location</Table.Cell>
              <Table.Cell>{valueOrDash(getOption(vpnState, 'location', 'Location'))}</Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Forwarded Port</Table.Cell>
              <Table.Cell>
                {valueOrDash(getOption(vpnState, 'forwardedPort', 'ForwardedPort'))}
              </Table.Cell>
            </Table.Row>
          </Table.Body>
        </Table>
        {forwards.length > 0 && (
          <Table
            celled
            compact
          >
            <Table.Header>
              <Table.Row>
                <Table.HeaderCell>Slot</Table.HeaderCell>
                <Table.HeaderCell>Protocol</Table.HeaderCell>
                <Table.HeaderCell>Public</Table.HeaderCell>
                <Table.HeaderCell>Local</Table.HeaderCell>
              </Table.Row>
            </Table.Header>
            <Table.Body>
              {forwards.map((forward) => (
                <Table.Row key={`${forward.slot}-${forward.proto}-${forward.publicPort}`}>
                  <Table.Cell>{forward.slot}</Table.Cell>
                  <Table.Cell>{forward.proto}</Table.Cell>
                  <Table.Cell>
                    {valueOrDash(forward.publicIPAddress || forward.publicIp)}:
                    {forward.publicPort}
                  </Table.Cell>
                  <Table.Cell>
                    {forward.localPort || '-'}
                    {forward.targetPort && forward.targetPort !== forward.publicPort
                      ? ` -> ${forward.targetPort}`
                      : ''}
                  </Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        )}
      </Card.Content>
    </Card>
  );
};

const LidarrPanel = ({ options }) => {
  const lidarrOptions = getLidarrOptions(options);
  const [status, setStatus] = useState(null);
  const [wanted, setWanted] = useState([]);
  const [syncResult, setSyncResult] = useState(null);
  const [importDirectory, setImportDirectory] = useState('');
  const [importResult, setImportResult] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState('');
  const enabled = getOption(lidarrOptions, 'enabled', 'Enabled');

  const maskedApiKey = useMemo(() => {
    const apiKey = getOption(lidarrOptions, 'apiKey', 'ApiKey');
    return apiKey ? 'Configured' : 'Not configured';
  }, [lidarrOptions]);

  const run = async (name, action) => {
    setLoading(name);
    setError('');

    try {
      await action();
    } catch (error) {
      setError(
        error?.response?.data ||
          error?.response?.statusText ||
          error?.message ||
          'Lidarr request failed',
      );
    } finally {
      setLoading('');
    }
  };

  return (
    <Card fluid>
      <Card.Content>
        <Card.Header>
          <Icon name="music" />
          Lidarr
        </Card.Header>
        <Card.Meta>Wanted-album sync and completed-download import bridge.</Card.Meta>
      </Card.Content>
      <Card.Content>
        <div className="integration-status-row">
          {boolLabel(enabled)}
          {boolLabel(
            getOption(lidarrOptions, 'syncWantedToWishlist', 'SyncWantedToWishlist'),
            'Wanted Sync',
            'Wanted Sync Off',
          )}
          {boolLabel(
            getOption(lidarrOptions, 'autoImportCompleted', 'AutoImportCompleted'),
            'Auto Import',
            'Auto Import Off',
          )}
          <Label>
            <Icon name={maskedApiKey === 'Configured' ? 'key' : 'warning sign'} />
            API Key {maskedApiKey}
          </Label>
        </div>
        <Table
          basic="very"
          compact
          definition
        >
          <Table.Body>
            <Table.Row>
              <Table.Cell>URL</Table.Cell>
              <Table.Cell>{valueOrDash(getOption(lidarrOptions, 'url', 'Url'))}</Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Timeout</Table.Cell>
              <Table.Cell>
                {valueOrDash(getOption(lidarrOptions, 'timeoutSeconds', 'TimeoutSeconds'))}
                {' s'}
              </Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Sync Interval</Table.Cell>
              <Table.Cell>
                {valueOrDash(getOption(lidarrOptions, 'syncIntervalSeconds', 'SyncIntervalSeconds'))}
                {' s'}
              </Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Import Mode</Table.Cell>
              <Table.Cell>{valueOrDash(getOption(lidarrOptions, 'importMode', 'ImportMode'))}</Table.Cell>
            </Table.Row>
            <Table.Row>
              <Table.Cell>Import Path Map</Table.Cell>
              <Table.Cell>
                {valueOrDash(getOption(lidarrOptions, 'importPathFrom', 'ImportPathFrom'))}
                {' -> '}
                {valueOrDash(getOption(lidarrOptions, 'importPathTo', 'ImportPathTo'))}
              </Table.Cell>
            </Table.Row>
          </Table.Body>
        </Table>
        {error && (
          <Message
            negative
            size="small"
          >
            {error}
          </Message>
        )}
        <div className="integration-actions">
          <Popup
            content="Fetch Lidarr system status using the configured URL and API key."
            trigger={
              <Button
                icon
                labelPosition="left"
                loading={loading === 'status'}
                onClick={() =>
                  run('status', async () => setStatus(await lidarr.getStatus()))
                }
              >
                <Icon name="heartbeat" />
                Check Status
              </Button>
            }
          />
          <Popup
            content="Preview Lidarr wanted albums that can be synced into slskdN Wishlist."
            trigger={
              <Button
                icon
                labelPosition="left"
                loading={loading === 'wanted'}
                onClick={() =>
                  run('wanted', async () =>
                    setWanted(await lidarr.getWantedMissing({ pageSize: 25 })),
                  )
                }
              >
                <Icon name="list" />
                Load Wanted
              </Button>
            }
          />
          <Popup
            content="Create or refresh slskdN Wishlist entries from Lidarr wanted albums."
            trigger={
              <Button
                icon
                labelPosition="left"
                loading={loading === 'sync'}
                onClick={() =>
                  run('sync', async () => setSyncResult(await lidarr.syncWanted()))
                }
                primary
              >
                <Icon name="sync" />
                Sync Wanted
              </Button>
            }
          />
        </div>
        {status && (
          <Message
            positive
            size="small"
          >
            Lidarr responded: {status.appName || status.AppName || 'Lidarr'}{' '}
            {status.version || status.Version || ''}
          </Message>
        )}
        {syncResult && (
          <Message
            info
            size="small"
          >
            Wanted sync: {syncResult.createdCount ?? syncResult.CreatedCount ?? 0} created,{' '}
            {syncResult.duplicateCount ?? syncResult.DuplicateCount ?? 0} duplicates,{' '}
            {syncResult.skippedCount ?? syncResult.SkippedCount ?? 0} skipped.
          </Message>
        )}
        {wanted.length > 0 && (
          <Table
            celled
            compact
          >
            <Table.Header>
              <Table.Row>
                <Table.HeaderCell>Artist</Table.HeaderCell>
                <Table.HeaderCell>Album</Table.HeaderCell>
              </Table.Row>
            </Table.Header>
            <Table.Body>
              {wanted.slice(0, 10).map((album) => (
                <Table.Row key={album.id || album.Id || `${album.title}-${album.foreignAlbumId}`}>
                  <Table.Cell>
                    {album.artist?.artistName || album.Artist?.ArtistName || '-'}
                  </Table.Cell>
                  <Table.Cell>{album.title || album.Title || '-'}</Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        )}
        <Segment className="integration-manual-import">
          <Header as="h4">Manual Import</Header>
          <Input
            action={{
              content: 'Import',
              disabled: !importDirectory.trim(),
              icon: 'download',
              loading: loading === 'import',
              onClick: () =>
                run('import', async () =>
                  setImportResult(
                    await lidarr.importCompletedDirectory({
                      directory: importDirectory.trim(),
                    }),
                  ),
                ),
            }}
            fluid
            onChange={(_, { value }) => setImportDirectory(value)}
            placeholder="Completed download directory visible to slskdN..."
            value={importDirectory}
          />
          {importResult && (
            <Message
              size="small"
              warning={Boolean(importResult.skippedReason || importResult.SkippedReason)}
            >
              {importResult.skippedReason || importResult.SkippedReason
                ? `Skipped: ${importResult.skippedReason || importResult.SkippedReason}`
                : `Queued Lidarr command ${importResult.commandId || importResult.CommandId || '-'}`}
            </Message>
          )}
        </Segment>
      </Card.Content>
    </Card>
  );
};

const MediaServerPanel = () => {
  const [localPath, setLocalPath] = useState('');
  const [serverPath, setServerPath] = useState('');
  const [remotePathFrom, setRemotePathFrom] = useState('');
  const [remotePathTo, setRemotePathTo] = useState('');
  const diagnostic = buildMediaServerPathDiagnostic({
    localPath,
    remotePathFrom,
    remotePathTo,
    serverPath,
  });

  return (
    <Card fluid>
      <Card.Content>
        <Card.Header>
          <Icon name="server" />
          Media Servers
        </Card.Header>
        <Card.Meta>
          Optional Plex, Jellyfin/Emby, and Navidrome integration planning and path diagnostics.
        </Card.Meta>
      </Card.Content>
      <Card.Content>
        <Card.Group
          itemsPerRow={3}
          stackable
        >
          {mediaServerAdapters.map((adapter) => (
            <Card
              className="media-server-adapter-card"
              key={adapter.id}
            >
              <Card.Content>
                <Card.Header>{adapter.label}</Card.Header>
                <Card.Meta>
                  {adapter.requiresToken ? 'Token required' : 'No token required'}
                </Card.Meta>
                <div className="integration-status-row">
                  {adapter.capabilities.map((capability) => (
                    <Label
                      basic
                      key={capability}
                      size="tiny"
                    >
                      {capability}
                    </Label>
                  ))}
                </div>
              </Card.Content>
            </Card>
          ))}
        </Card.Group>

        <Segment className="integration-manual-import">
          <Header as="h4">Path Diagnostics</Header>
          <p>
            Check whether a completed file path reported by slskdN maps to the
            path a media server can scan.
          </p>
          <div className="media-server-path-grid">
            <Input
              aria-label="slskdN local file path"
              fluid
              label="slskdN path"
              onChange={(_, { value }) => setLocalPath(value)}
              placeholder="/downloads/complete/Artist/Album/track.flac"
              value={localPath}
            />
            <Input
              aria-label="Media server file path"
              fluid
              label="Server path"
              onChange={(_, { value }) => setServerPath(value)}
              placeholder="/library/music/Artist/Album/track.flac"
              value={serverPath}
            />
            <Input
              aria-label="Remote path map from"
              fluid
              label="Map from"
              onChange={(_, { value }) => setRemotePathFrom(value)}
              placeholder="/downloads/complete"
              value={remotePathFrom}
            />
            <Input
              aria-label="Remote path map to"
              fluid
              label="Map to"
              onChange={(_, { value }) => setRemotePathTo(value)}
              placeholder="/library/music"
              value={remotePathTo}
            />
          </div>
          <Message
            color={diagnostic.color}
            size="small"
          >
            <Message.Header>{diagnostic.status}</Message.Header>
            <p>{diagnostic.message}</p>
            {diagnostic.mappedPath && <p>Mapped path: {diagnostic.mappedPath}</p>}
          </Message>
        </Segment>
      </Card.Content>
    </Card>
  );
};

const ServarrReadinessPanel = ({ options }) => {
  const lidarrOptions = getLidarrOptions(options);
  const checks = buildServarrReadiness({
    apiKey: getOption(lidarrOptions, 'apiKey', 'ApiKey'),
    autoImportCompleted: getOption(
      lidarrOptions,
      'autoImportCompleted',
      'AutoImportCompleted',
    ),
    enabled: getOption(lidarrOptions, 'enabled', 'Enabled'),
    importPathFrom: getOption(lidarrOptions, 'importPathFrom', 'ImportPathFrom'),
    importPathTo: getOption(lidarrOptions, 'importPathTo', 'ImportPathTo'),
    syncWantedToWishlist: getOption(
      lidarrOptions,
      'syncWantedToWishlist',
      'SyncWantedToWishlist',
    ),
    url: getOption(lidarrOptions, 'url', 'Url'),
  });
  const summary = summarizeServarrReadiness(checks);

  return (
    <Card fluid>
      <Card.Content>
        <Card.Header>
          <Icon name="settings" />
          Servarr Setup
        </Card.Header>
        <Card.Meta>
          Local readiness checklist for indexer/download-client style integration.
        </Card.Meta>
      </Card.Content>
      <Card.Content>
        <div className="integration-status-row">
          <Label color={summary.status === 'Ready' ? 'green' : 'orange'}>
            <Icon name={summary.status === 'Ready' ? 'check circle' : 'warning sign'} />
            {summary.status}
          </Label>
          <Label>
            {summary.ready}/{summary.total} checks ready
          </Label>
        </div>
        <Table
          celled
          compact
        >
          <Table.Header>
            <Table.Row>
              <Table.HeaderCell>Check</Table.HeaderCell>
              <Table.HeaderCell>Status</Table.HeaderCell>
              <Table.HeaderCell>Why it matters</Table.HeaderCell>
            </Table.Row>
          </Table.Header>
          <Table.Body>
            {checks.map((check) => (
              <Table.Row key={check.id}>
                <Table.Cell>{check.title}</Table.Cell>
                <Table.Cell>
                  {boolLabel(check.ready, 'Ready', 'Needs Setup')}
                </Table.Cell>
                <Table.Cell>{check.description}</Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
        <Message
          info
          size="small"
        >
          This checklist is diagnostic only. It does not register indexers,
          create download clients, pull wanted items, or trigger imports.
        </Message>
      </Card.Content>
    </Card>
  );
};

const Integrations = ({ options = {}, state = {} }) => (
  <div className="integrations-admin">
    <Segment>
      <Header as="h3">
        <Icon name="plug" />
        Integrations
      </Header>
      <p>
        Operational status and admin actions for integrations that affect
        connection routing, downloads, and external media managers.
      </p>
    </Segment>
    <VpnPanel
      options={options}
      state={state}
    />
    <LidarrPanel options={options} />
    <SourceFeedIntegrationsPanel options={options} />
    <ServarrReadinessPanel options={options} />
    <MediaServerPanel />
  </div>
);

export default Integrations;
