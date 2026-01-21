import * as mesh from '../../../lib/mesh';
import React, { useEffect, useState } from 'react';
import {
  Card,
  Grid,
  Header,
  Icon,
  Label,
  List,
  Loader,
  Message,
  Segment,
  Statistic,
} from 'semantic-ui-react';

const Mesh = () => {
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    const fetchStats = async () => {
      try {
        setLoading(true);
        setError(null);
        const data = await mesh.getStats();
        setStats(data);
      } catch (error_) {
        setError(error_.message);
      } finally {
        setLoading(false);
      }
    };

    fetchStats();

    // Refresh stats every 30 seconds
    const interval = setInterval(fetchStats, 30_000);
    return () => clearInterval(interval);
  }, []);

  const getHealthColor = (status) => {
    switch (status) {
      case 'Healthy':
        return 'green';
      case 'Degraded':
        return 'yellow';
      case 'Unhealthy':
        return 'red';
      default:
        return 'grey';
    }
  };

  const getHealthIcon = (status) => {
    switch (status) {
      case 'Healthy':
        return 'checkmark';
      case 'Degraded':
        return 'warning sign';
      case 'Unhealthy':
        return 'remove';
      default:
        return 'question';
    }
  };

  if (loading && !stats) {
    return (
      <Segment>
        <Loader
          active
          inline="centered"
        >
          Loading mesh statistics...
        </Loader>
      </Segment>
    );
  }

  if (error) {
    return (
      <Message error>
        <Message.Header>Failed to load mesh statistics</Message.Header>
        <p>{error}</p>
      </Message>
    );
  }

  return (
    <div>
      <Header as="h2">
        <Icon name="sitemap" />
        Mesh Network Status
      </Header>

      <Grid stackable>
        {/* Overall Health Status */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon
                  color={getHealthColor(stats?.status)}
                  name={getHealthIcon(stats?.status)}
                />
                Network Health: {stats?.status || 'Unknown'}
              </Card.Header>
              <Card.Description>
                {stats?.description || 'No health information available'}
              </Card.Description>
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Key Statistics */}
        <Grid.Column width={16}>
          <Segment>
            <Header as="h3">Network Statistics</Header>
            <Statistic.Group size="small">
              <Statistic>
                <Statistic.Value>{stats?.totalPeers || 0}</Statistic.Value>
                <Statistic.Label>Total Peers</Statistic.Label>
              </Statistic>
              <Statistic>
                <Statistic.Value>
                  {stats?.activeDhtSessions || 0}
                </Statistic.Value>
                <Statistic.Label>DHT Sessions</Statistic.Label>
              </Statistic>
              <Statistic>
                <Statistic.Value>
                  {stats?.activeOverlaySessions || 0}
                </Statistic.Value>
                <Statistic.Label>Overlay Sessions</Statistic.Label>
              </Statistic>
              <Statistic>
                <Statistic.Value>
                  {stats?.routingTableSize || 0}
                </Statistic.Value>
                <Statistic.Label>Routing Table Size</Statistic.Label>
              </Statistic>
            </Statistic.Group>
          </Segment>
        </Grid.Column>

        {/* Connection Details */}
        <Grid.Column width={8}>
          <Segment>
            <Header as="h3">
              <Icon name="plug" />
              Connections
            </Header>
            <List
              divided
              relaxed
            >
              <List.Item>
                <List.Content>
                  <List.Header>DHT Nodes</List.Header>
                  <List.Description>
                    {stats?.activeDhtSessions || 0} active connections
                  </List.Description>
                </List.Content>
              </List.Item>
              <List.Item>
                <List.Content>
                  <List.Header>Overlay Sessions</List.Header>
                  <List.Description>
                    {stats?.activeOverlaySessions || 0} active sessions
                  </List.Description>
                </List.Content>
              </List.Item>
              <List.Item>
                <List.Content>
                  <List.Header>Mirrored Sessions</List.Header>
                  <List.Description>
                    {stats?.activeMirroredSessions || 0} relay connections
                  </List.Description>
                </List.Content>
              </List.Item>
              <List.Item>
                <List.Content>
                  <List.Header>Bootstrap Peers</List.Header>
                  <List.Description>
                    {stats?.bootstrapPeers || 0} bootstrap nodes
                  </List.Description>
                </List.Content>
              </List.Item>
            </List>
          </Segment>
        </Grid.Column>

        {/* Performance Metrics */}
        <Grid.Column width={8}>
          <Segment>
            <Header as="h3">
              <Icon name="chart line" />
              Performance
            </Header>
            <List
              divided
              relaxed
            >
              <List.Item>
                <List.Content>
                  <List.Header>DHT Operations/sec</List.Header>
                  <List.Description>
                    {stats?.dhtOperationsPerSecond?.toFixed(1) || '0.0'} ops/sec
                  </List.Description>
                </List.Content>
              </List.Item>
              <List.Item>
                <List.Content>
                  <List.Header>Messages Sent</List.Header>
                  <List.Description>
                    {stats?.messagesSent || 0} total messages
                  </List.Description>
                </List.Content>
              </List.Item>
              <List.Item>
                <List.Content>
                  <List.Header>Messages Received</List.Header>
                  <List.Description>
                    {stats?.messagesReceived || 0} total messages
                  </List.Description>
                </List.Content>
              </List.Item>
              <List.Item>
                <List.Content>
                  <List.Header>Peer Churn Events</List.Header>
                  <List.Description>
                    {stats?.peerChurnEvents || 0} churn events
                  </List.Description>
                </List.Content>
              </List.Item>
            </List>
          </Segment>
        </Grid.Column>

        {/* NAT and Network Info */}
        <Grid.Column width={16}>
          <Segment>
            <Header as="h3">
              <Icon name="shield" />
              Network Configuration
            </Header>
            <Grid>
              <Grid.Column width={8}>
                <List>
                  <List.Item>
                    <List.Content>
                      <List.Header>NAT Type</List.Header>
                      <List.Description>
                        <Label
                          color={
                            stats?.natType === 'Direct' ? 'green' : 'yellow'
                          }
                        >
                          {stats?.natType || 'Unknown'}
                        </Label>
                      </List.Description>
                    </List.Content>
                  </List.Item>
                </List>
              </Grid.Column>
              <Grid.Column width={8}>
                <List>
                  <List.Item>
                    <List.Content>
                      <List.Header>Health Indicators</List.Header>
                      <List.Description>
                        <Label
                          color={stats?.routingTableHealthy ? 'green' : 'red'}
                        >
                          Routing Table:{' '}
                          {stats?.routingTableHealthy ? 'Healthy' : 'Unhealthy'}
                        </Label>
                        <br />
                        <Label
                          color={
                            stats?.peerConnectivityHealthy ? 'green' : 'red'
                          }
                        >
                          Peer Connectivity:{' '}
                          {stats?.peerConnectivityHealthy
                            ? 'Healthy'
                            : 'Unhealthy'}
                        </Label>
                        <br />
                        <Label
                          color={stats?.messageFlowHealthy ? 'green' : 'red'}
                        >
                          Message Flow:{' '}
                          {stats?.messageFlowHealthy ? 'Healthy' : 'Unhealthy'}
                        </Label>
                      </List.Description>
                    </List.Content>
                  </List.Item>
                </List>
              </Grid.Column>
            </Grid>
          </Segment>
        </Grid.Column>
      </Grid>
    </div>
  );
};

export default Mesh;
