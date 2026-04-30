import * as lidarr from '../../../lib/lidarr';
import {
  buildMediaServerPathDiagnostic,
  mediaServerAdapters,
} from '../../../lib/mediaServerIntegrations';
import {
  buildServarrReadiness,
  summarizeServarrReadiness,
} from '../../../lib/servarrReadiness';
import React, { useMemo, useState } from 'react';
import {
  Button,
  Card,
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

const getVpnState = (state = {}) => getOption(state, 'vpn', 'Vpn', 'VPN') || {};

const boolLabel = (value, trueText = 'Enabled', falseText = 'Disabled') => (
  <Label color={value ? 'green' : 'grey'}>
    <Icon name={value ? 'check circle' : 'minus circle'} />
    {value ? trueText : falseText}
  </Label>
);

const valueOrDash = (value) =>
  value === undefined || value === null || value === '' ? '-' : value;

const portForwards = (vpn = {}) =>
  getOption(vpn, 'portForwards', 'PortForwards') || [];

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
    <ServarrReadinessPanel options={options} />
    <MediaServerPanel />
  </div>
);

export default Integrations;
