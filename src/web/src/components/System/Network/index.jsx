import * as slskdnAPI from '../../../lib/slskdn';
import { LoaderSegment, ShrinkableButton } from '../../Shared';
import React, { useCallback, useEffect, useState } from 'react';
import { toast } from 'react-toastify';
import {
  Card,
  Divider,
  Grid,
  Header,
  Icon,
  Label,
  List,
  Progress,
  Segment,
  Statistic,
  Table,
} from 'semantic-ui-react';

const formatBytes = (bytes) => {
  if (bytes === 0 || bytes === undefined || bytes === null) return '0 B';
  const k = 1_024;
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
  const index = Math.floor(Math.log(bytes) / Math.log(k));
  return (
    Number.parseFloat((bytes / k ** index).toFixed(1)) + ' ' + sizes[index]
  );
};

const formatNumber = (value) => {
  if (value === undefined || value === null) return '0';
  if (value >= 1_000_000) return (value / 1_000_000).toFixed(1) + 'M';
  if (value >= 1_000) return (value / 1_000).toFixed(1) + 'K';
  return value.toString();
};

const formatTimeAgo = (dateString) => {
  if (!dateString) return 'never';
  const date = new Date(dateString);
  const now = new Date();
  const seconds = Math.floor((now - date) / 1_000);

  if (seconds < 60) return `${seconds}s ago`;
  if (seconds < 3_600) return `${Math.floor(seconds / 60)}m ago`;
  if (seconds < 86_400) return `${Math.floor(seconds / 3_600)}h ago`;
  return `${Math.floor(seconds / 86_400)}d ago`;
};

const StatCard = ({ color, icon, label, subLabel, value }) => (
  <Card>
    <Card.Content>
      <Card.Header>
        <Icon
          color={color}
          name={icon}
        />{' '}
        {value}
      </Card.Header>
      <Card.Meta>{label}</Card.Meta>
      {subLabel && <Card.Description>{subLabel}</Card.Description>}
    </Card.Content>
  </Card>
);

// eslint-disable-next-line complexity
const Network = () => {
  const [loading, setLoading] = useState(true);
  const [stats, setStats] = useState({});
  const [meshPeers, setMeshPeers] = useState([]);
  const [discoveredPeers, setDiscoveredPeers] = useState([]);
  const [syncing, setSyncing] = useState({});

  const fetchData = useCallback(async () => {
    try {
      const [statsData, peersData, discoveredData] = await Promise.all([
        slskdnAPI.getSlskdnStats().catch(() => ({})),
        slskdnAPI.getMeshPeers().catch(() => []),
        slskdnAPI.getDiscoveredPeers().catch(() => []),
      ]);

      setStats(statsData || {});
      setMeshPeers(Array.isArray(peersData) ? peersData : []);
      setDiscoveredPeers(Array.isArray(discoveredData) ? discoveredData : []);
    } catch (error) {
      console.error('Failed to fetch network stats:', error);
      // Don't show toast on every poll failure
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
    const interval = setInterval(fetchData, 5_000); // Refresh every 5 seconds
    return () => clearInterval(interval);
  }, [fetchData]);

  const handleSync = async (username) => {
    setSyncing((previous) => ({ ...previous, [username]: true }));
    try {
      await slskdnAPI.triggerMeshSync(username);
      toast.success(`Sync initiated with ${username}`);
    } catch {
      toast.error(`Failed to sync with ${username}`);
    } finally {
      setSyncing((previous) => ({ ...previous, [username]: false }));
    }
  };

  if (loading) {
    return <LoaderSegment />;
  }

  const { backfill, capabilities, hashDb, mesh, swarmJobs } = stats;

  return (
    <div className="network-dashboard">
      {/* Header Stats */}
      <Card.Group
        itemsPerRow={4}
        stackable
      >
        <StatCard
          color="blue"
          icon="sitemap"
          label="Mesh Peers"
          subLabel="slskdn clients connected"
          value={mesh?.connectedPeerCount ?? meshPeers.length ?? 0}
        />
        <StatCard
          color="green"
          icon="database"
          label="Hash Entries"
          subLabel={
            hashDb?.dbSizeBytes
              ? `${formatBytes(hashDb.dbSizeBytes)} on disk`
              : 'Local database'
          }
          value={formatNumber(hashDb?.totalEntries ?? 0)}
        />
        <StatCard
          color="purple"
          icon="sync"
          label="Sequence ID"
          subLabel="Mesh sync position"
          value={hashDb?.currentSeqId ?? mesh?.localSeqId ?? 0}
        />
        <StatCard
          color="orange"
          icon="bolt"
          label="Active Swarms"
          subLabel="Multi-source downloads"
          value={swarmJobs?.length ?? 0}
        />
      </Card.Group>

      <Divider />

      {/* Our Capabilities */}
      <Segment>
        <Header as="h4">
          <Icon name="id card" />
          <Header.Content>
            Our Capabilities
            <Header.Subheader>
              What we advertise to other slskdn peers
            </Header.Subheader>
          </Header.Content>
        </Header>

        <Label.Group>
          <Label color="blue">
            <Icon name="code branch" />
            {capabilities?.version ?? 'slskdn'}
          </Label>
          {capabilities?.features?.map((feature) => (
            <Label
              color="teal"
              key={feature}
            >
              <Icon name="check" />
              {feature}
            </Label>
          )) ?? (
            <>
              <Label color="teal">
                <Icon name="check" />
                multi_source
              </Label>
              <Label color="teal">
                <Icon name="check" />
                hash_db
              </Label>
              <Label color="teal">
                <Icon name="check" />
                mesh_sync
              </Label>
            </>
          )}
        </Label.Group>
      </Segment>

      <Grid
        columns={2}
        stackable
      >
        {/* Mesh Peers */}
        <Grid.Column>
          <Segment>
            <Header as="h4">
              <Icon name="sitemap" />
              <Header.Content>
                Mesh Peers
                <Header.Subheader>
                  Connected slskdn clients for hash sync
                </Header.Subheader>
              </Header.Content>
            </Header>

            {meshPeers.length === 0 ? (
              <Segment
                basic
                placeholder
                textAlign="center"
              >
                <Header icon>
                  <Icon name="users" />
                  No mesh peers connected
                </Header>
                <p>Other slskdn clients will appear here when discovered</p>
              </Segment>
            ) : (
              <Table
                basic="very"
                compact
              >
                <Table.Header>
                  <Table.Row>
                    <Table.HeaderCell>Peer</Table.HeaderCell>
                    <Table.HeaderCell>Seq ID</Table.HeaderCell>
                    <Table.HeaderCell>Last Sync</Table.HeaderCell>
                    <Table.HeaderCell>Actions</Table.HeaderCell>
                  </Table.Row>
                </Table.Header>
                <Table.Body>
                  {meshPeers.map((peer) => (
                    <Table.Row key={peer.username}>
                      <Table.Cell>
                        <Icon
                          color="green"
                          name="circle"
                          size="tiny"
                        />{' '}
                        {peer.username}
                      </Table.Cell>
                      <Table.Cell>{peer.lastSeqId ?? '-'}</Table.Cell>
                      <Table.Cell>{formatTimeAgo(peer.lastSyncAt)}</Table.Cell>
                      <Table.Cell>
                        <ShrinkableButton
                          compact
                          disabled={syncing[peer.username]}
                          icon="sync"
                          loading={syncing[peer.username]}
                          mediaQuery="(max-width: 500px)"
                          onClick={() => handleSync(peer.username)}
                          primary
                          size="mini"
                        >
                          Sync
                        </ShrinkableButton>
                      </Table.Cell>
                    </Table.Row>
                  ))}
                </Table.Body>
              </Table>
            )}
          </Segment>
        </Grid.Column>

        {/* Discovered Peers */}
        <Grid.Column>
          <Segment>
            <Header as="h4">
              <Icon name="search" />
              <Header.Content>
                Discovered slskdn Peers
                <Header.Subheader>
                  Peers with slskdn capabilities detected
                </Header.Subheader>
              </Header.Content>
            </Header>

            {discoveredPeers.length === 0 ? (
              <Segment
                basic
                placeholder
                textAlign="center"
              >
                <Header icon>
                  <Icon name="radar" />
                  No slskdn peers discovered yet
                </Header>
                <p>Peers are discovered through searches and downloads</p>
              </Segment>
            ) : (
              <List
                divided
                relaxed
              >
                {discoveredPeers.slice(0, 10).map((peer) => (
                  <List.Item key={peer.username}>
                    <List.Icon
                      color="blue"
                      name="user"
                      verticalAlign="middle"
                    />
                    <List.Content>
                      <List.Header>{peer.username}</List.Header>
                      <List.Description>
                        {peer.version ?? 'slskdn'} • Last seen:{' '}
                        {formatTimeAgo(peer.lastSeenAt)}
                      </List.Description>
                    </List.Content>
                  </List.Item>
                ))}
                {discoveredPeers.length > 10 && (
                  <List.Item>
                    <List.Content>
                      <em>...and {discoveredPeers.length - 10} more</em>
                    </List.Content>
                  </List.Item>
                )}
              </List>
            )}
          </Segment>
        </Grid.Column>
      </Grid>

      <Divider />

      {/* Hash Database Details */}
      <Segment>
        <Header as="h4">
          <Icon name="database" />
          <Header.Content>
            Hash Database
            <Header.Subheader>
              Content-addressed FLAC fingerprints
            </Header.Subheader>
          </Header.Content>
        </Header>

        <Statistic.Group
          size="tiny"
          widths={4}
        >
          <Statistic>
            <Statistic.Value>
              {formatNumber(hashDb?.totalEntries ?? 0)}
            </Statistic.Value>
            <Statistic.Label>Total Entries</Statistic.Label>
          </Statistic>
          <Statistic>
            <Statistic.Value>
              {formatNumber(hashDb?.uniqueFiles ?? hashDb?.totalEntries ?? 0)}
            </Statistic.Value>
            <Statistic.Label>Unique Files</Statistic.Label>
          </Statistic>
          <Statistic>
            <Statistic.Value>
              {formatBytes(hashDb?.dbSizeBytes ?? 0)}
            </Statistic.Value>
            <Statistic.Label>Database Size</Statistic.Label>
          </Statistic>
          <Statistic>
            <Statistic.Value>{hashDb?.currentSeqId ?? 0}</Statistic.Value>
            <Statistic.Label>Sequence ID</Statistic.Label>
          </Statistic>
        </Statistic.Group>

        {hashDb?.coveragePercent !== undefined && (
          <>
            <Divider hidden />
            <Progress
              color="green"
              percent={hashDb.coveragePercent}
              progress
              size="small"
            >
              Coverage of shared FLACs
            </Progress>
          </>
        )}
      </Segment>

      {/* Backfill Scheduler */}
      <Segment>
        <Header as="h4">
          <Icon name="clock" />
          <Header.Content>
            Backfill Scheduler
            <Header.Subheader>
              Conservative discovery of hashes from non-slskdn peers
            </Header.Subheader>
          </Header.Content>
        </Header>

        <Grid
          columns={4}
          stackable
        >
          <Grid.Column>
            <Statistic size="mini">
              <Statistic.Value>
                <Icon
                  color={backfill?.isActive ? 'green' : 'grey'}
                  name="circle"
                />{' '}
                {backfill?.isActive ? 'Active' : 'Idle'}
              </Statistic.Value>
              <Statistic.Label>Status</Statistic.Label>
            </Statistic>
          </Grid.Column>
          <Grid.Column>
            <Statistic size="mini">
              <Statistic.Value>{backfill?.pendingCount ?? 0}</Statistic.Value>
              <Statistic.Label>Pending Files</Statistic.Label>
            </Statistic>
          </Grid.Column>
          <Grid.Column>
            <Statistic size="mini">
              <Statistic.Value>{backfill?.completedToday ?? 0}</Statistic.Value>
              <Statistic.Label>Completed Today</Statistic.Label>
            </Statistic>
          </Grid.Column>
          <Grid.Column>
            <Statistic size="mini">
              <Statistic.Value>
                {backfill?.discoveryRate ?? 0}/hr
              </Statistic.Value>
              <Statistic.Label>Discovery Rate</Statistic.Label>
            </Statistic>
          </Grid.Column>
        </Grid>
      </Segment>

      {/* Active Swarm Downloads */}
      {swarmJobs && swarmJobs.length > 0 && (
        <Segment>
          <Header as="h4">
            <Icon name="bolt" />
            <Header.Content>
              Active Swarm Downloads
              <Header.Subheader>
                Multi-source downloads in progress
              </Header.Subheader>
            </Header.Content>
          </Header>

          {swarmJobs.map((job) => (
            <Card
              fluid
              key={job.jobId}
            >
              <Card.Content>
                <Card.Header>
                  <Icon
                    color="yellow"
                    name="bolt"
                  />
                  {job.filename?.split('/').pop() ?? 'Unknown file'}
                </Card.Header>
                <Card.Meta>
                  {job.activeSources ?? 0} sources •{' '}
                  {formatBytes(job.downloadedBytes ?? 0)} /{' '}
                  {formatBytes(job.totalBytes ?? 0)}
                </Card.Meta>
                <Progress
                  active
                  color="blue"
                  percent={job.progressPercent ?? 0}
                  progress
                  size="small"
                />
                {job.workers && job.workers.length > 0 && (
                  <List
                    horizontal
                    size="small"
                  >
                    {job.workers.slice(0, 5).map((worker) => (
                      <List.Item key={worker.username}>
                        <Label size="tiny">
                          <Icon name="user" />
                          {worker.username}
                          <Label.Detail>
                            {formatBytes(worker.speedBps ?? 0)}/s
                          </Label.Detail>
                        </Label>
                      </List.Item>
                    ))}
                    {job.workers.length > 5 && (
                      <List.Item>
                        <Label size="tiny">
                          +{job.workers.length - 5} more
                        </Label>
                      </List.Item>
                    )}
                  </List>
                )}
              </Card.Content>
            </Card>
          ))}
        </Segment>
      )}
    </div>
  );
};

export default Network;
