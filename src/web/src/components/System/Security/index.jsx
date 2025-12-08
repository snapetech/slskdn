import './Security.css';
import React, { useCallback, useEffect, useState } from 'react';
import {
  Button,
  Dimmer,
  Header,
  Icon,
  Loader,
  Message,
  Segment,
  Statistic,
} from 'semantic-ui-react';
import * as securityApi from '../../../lib/security';

const Security = () => {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [dashboard, setDashboard] = useState(null);
  const [refreshing, setRefreshing] = useState(false);

  const fetchData = useCallback(async () => {
    try {
      setRefreshing(true);
      const dashboardData = await securityApi.getDashboard().catch(() => null);
      setDashboard(dashboardData);
      setError(null);
    } catch (fetchError) {
      setError(fetchError.message || 'Failed to load security data');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
    const interval = setInterval(fetchData, 30_000);
    return () => clearInterval(interval);
  }, [fetchData]);

  if (loading) {
    return (
      <Segment placeholder>
        <Dimmer
          active
          inverted
        >
          <Loader>Loading Security Status...</Loader>
        </Dimmer>
      </Segment>
    );
  }

  if (error && !dashboard) {
    return (
      <Message negative>
        <Message.Header>Security Module Unavailable</Message.Header>
        <p>{error}</p>
        <p>Security features may not be enabled on this server.</p>
      </Message>
    );
  }

  const stats = dashboard || {};

  return (
    <div className="security-dashboard">
      <div className="security-header">
        <Header as="h3">
          <Icon name="shield alternate" />
          <Header.Content>
            Security Status
            <Header.Subheader>Real-time security monitoring</Header.Subheader>
          </Header.Content>
        </Header>
        <Button
          icon="refresh"
          loading={refreshing}
          onClick={fetchData}
          size="tiny"
          title="Refresh"
        />
      </div>

      <Statistic.Group
        size="small"
        widths={4}
      >
        <Statistic color="blue">
          <Statistic.Value>
            {stats.networkGuardStats?.globalConnections ?? 0}
          </Statistic.Value>
          <Statistic.Label>Active Connections</Statistic.Label>
        </Statistic>
        <Statistic color="teal">
          <Statistic.Value>
            {stats.reputationStats?.totalPeers ?? 0}
          </Statistic.Value>
          <Statistic.Label>Tracked Peers</Statistic.Label>
        </Statistic>
        <Statistic color="orange">
          <Statistic.Value>
            {stats.violationStats?.trackedIps ?? 0}
          </Statistic.Value>
          <Statistic.Label>Tracked Violators</Statistic.Label>
        </Statistic>
        <Statistic color="green">
          <Statistic.Value>
            {stats.eventStats?.totalEvents ?? 0}
          </Statistic.Value>
          <Statistic.Label>Security Events</Statistic.Label>
        </Statistic>
      </Statistic.Group>

      <Segment>
        <Header as="h4">
          <Icon name="info circle" />
          Security Overview
        </Header>
        <p>
          <strong>Network Guard:</strong> Rate limiting and connection caps are{' '}
          {stats.networkGuardStats ? 'active' : 'inactive'}.
        </p>
        <p>
          <strong>Peer Reputation:</strong>{' '}
          {stats.reputationStats?.trustedPeers ?? 0} trusted,{' '}
          {stats.reputationStats?.untrustedPeers ?? 0} untrusted peers.
        </p>
        <p>
          <strong>Violations:</strong>{' '}
          {stats.violationStats?.trackedIps ?? 0} IPs,{' '}
          {stats.violationStats?.trackedUsernames ?? 0} usernames tracked.
        </p>
        <p>
          <strong>Crypto Health:</strong> Entropy checks:{' '}
          {stats.entropyStats?.checkCount ?? 0}, Warnings:{' '}
          {stats.entropyStats?.warningCount ?? 0}
        </p>
      </Segment>
    </div>
  );
};

export default Security;
