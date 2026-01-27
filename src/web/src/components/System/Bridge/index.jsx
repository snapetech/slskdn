import * as bridge from '../../../lib/bridge';
import React, { useEffect, useState } from 'react';
import {
  Button,
  Card,
  Checkbox,
  Form,
  Grid,
  Header,
  Icon,
  Input,
  Label,
  List,
  Loader,
  Message,
  Segment,
  Statistic,
  Table,
} from 'semantic-ui-react';

const Bridge = () => {
  const [config, setConfig] = useState(null);
  const [dashboard, setDashboard] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);

  useEffect(() => {
    const fetchData = async () => {
      try {
        setLoading(true);
        setError(null);
        const [configData, dashboardData] = await Promise.all([
          bridge.getConfig(),
          bridge.getDashboard(),
        ]);
        setConfig(configData);
        setDashboard(dashboardData);
      } catch (error_) {
        setError(error_.message);
      } finally {
        setLoading(false);
      }
    };

    fetchData();

    // Refresh dashboard every 10 seconds
    const interval = setInterval(async () => {
      try {
        const dashboardData = await bridge.getDashboard();
        setDashboard(dashboardData);
      } catch (error_) {
        // Silently fail on refresh
      }
    }, 10_000);

    return () => clearInterval(interval);
  }, []);

  const handleConfigChange = (field, value) => {
    setConfig((prev) => ({
      ...prev,
      [field]: value,
    }));
  };

  const handleSaveConfig = async () => {
    try {
      setSaving(true);
      setError(null);
      setSuccess(null);
      await bridge.updateConfig(config);
      setSuccess('Configuration updated. Restart bridge service to apply changes.');
    } catch (error_) {
      setError(error_.message);
    } finally {
      setSaving(false);
    }
  };

  const handleStartBridge = async () => {
    try {
      setError(null);
      await bridge.startBridge();
      // Refresh dashboard
      const dashboardData = await bridge.getDashboard();
      setDashboard(dashboardData);
    } catch (error_) {
      setError(error_.message);
    }
  };

  const handleStopBridge = async () => {
    try {
      setError(null);
      await bridge.stopBridge();
      // Refresh dashboard
      const dashboardData = await bridge.getDashboard();
      setDashboard(dashboardData);
    } catch (error_) {
      setError(error_.message);
    }
  };

  if (loading && !config) {
    return (
      <Segment>
        <Loader
          active
          inline="centered"
        >
          Loading bridge configuration...
        </Loader>
      </Segment>
    );
  }

  const health = dashboard?.health;
  const stats = dashboard?.stats;
  const clients = dashboard?.connectedClients || [];
  const meshBenefits = dashboard?.meshBenefits;

  return (
    <div>
      <Header as="h2">
        <Icon name="exchange" />
        Legacy Client Bridge
      </Header>

      {error && (
        <Message error>
          <Message.Header>Error</Message.Header>
          <p>{error}</p>
        </Message>
      )}

      {success && (
        <Message success>
          <Message.Header>Success</Message.Header>
          <p>{success}</p>
        </Message>
      )}

      <Grid stackable>
        {/* Configuration */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="cog" />
                Configuration
              </Card.Header>
            </Card.Content>
            <Card.Content>
              <Form>
                <Form.Group widths="equal">
                  <Form.Field>
                    <Checkbox
                      checked={config?.enabled || false}
                      label="Enable Bridge"
                      onChange={(e, { checked }) => handleConfigChange('enabled', checked)}
                      toggle
                    />
                    <small>Allow legacy Soulseek clients to connect via bridge</small>
                  </Form.Field>
                </Form.Group>
                <Form.Group widths="equal">
                  <Form.Field>
                    <label>Port</label>
                    <Input
                      disabled={!config?.enabled}
                      onChange={(e, { value }) => handleConfigChange('port', parseInt(value, 10) || 2242)}
                      type="number"
                      value={config?.port || 2242}
                    />
                    <small>Soulseek protocol port (default: 2242)</small>
                  </Form.Field>
                  <Form.Field>
                    <label>Soulfind Path</label>
                    <Input
                      disabled={!config?.enabled}
                      onChange={(e, { value }) => handleConfigChange('soulfind_path', value)}
                      placeholder="soulfind"
                      value={config?.soulfind_path || 'soulfind'}
                    />
                    <small>Path to Soulfind binary</small>
                  </Form.Field>
                </Form.Group>
                <Form.Group widths="equal">
                  <Form.Field>
                    <label>Max Clients</label>
                    <Input
                      disabled={!config?.enabled}
                      max={50}
                      min={1}
                      onChange={(e, { value }) => handleConfigChange('max_clients', parseInt(value, 10) || 10)}
                      type="number"
                      value={config?.max_clients || 10}
                    />
                    <small>Maximum concurrent legacy clients</small>
                  </Form.Field>
                  <Form.Field>
                    <Checkbox
                      checked={config?.require_auth || false}
                      disabled={!config?.enabled}
                      label="Require Authentication"
                      onChange={(e, { checked }) => handleConfigChange('require_auth', checked)}
                      toggle
                    />
                    <small>Require password for bridge connections</small>
                  </Form.Field>
                </Form.Group>
                <Button
                  loading={saving}
                  onClick={handleSaveConfig}
                  primary
                >
                  Save Configuration
                </Button>
              </Form>
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Service Control */}
        <Grid.Column width={16}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="power" />
                Service Control
              </Card.Header>
            </Card.Content>
            <Card.Content>
              <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
                <Button
                  color="green"
                  disabled={health?.isHealthy}
                  onClick={handleStartBridge}
                >
                  <Icon name="play" />
                  Start Bridge
                </Button>
                <Button
                  color="red"
                  disabled={!health?.isHealthy}
                  onClick={handleStopBridge}
                >
                  <Icon name="stop" />
                  Stop Bridge
                </Button>
                <Label
                  color={health?.isHealthy ? 'green' : 'red'}
                  size="large"
                >
                  <Icon name={health?.isHealthy ? 'checkmark' : 'remove'} />
                  {health?.isHealthy ? 'Running' : 'Stopped'}
                </Label>
                {health?.version && (
                  <Label>
                    Version: {health.version}
                  </Label>
                )}
              </div>
            </Card.Content>
          </Card>
        </Grid.Column>

        {/* Statistics */}
        {stats && (
          <Grid.Column width={16}>
            <Card fluid>
              <Card.Content>
                <Card.Header>
                  <Icon name="chart bar" />
                  Statistics
                </Card.Header>
              </Card.Content>
              <Card.Content>
                <Statistic.Group size="small">
                  <Statistic>
                    <Statistic.Value>{stats.currentConnections || 0}</Statistic.Value>
                    <Statistic.Label>Active Connections</Statistic.Label>
                  </Statistic>
                  <Statistic>
                    <Statistic.Value>{stats.totalSearches || 0}</Statistic.Value>
                    <Statistic.Label>Total Searches</Statistic.Label>
                  </Statistic>
                  <Statistic>
                    <Statistic.Value>{stats.totalDownloads || 0}</Statistic.Value>
                    <Statistic.Label>Total Downloads</Statistic.Label>
                  </Statistic>
                  <Statistic>
                    <Statistic.Value>{(stats.totalBytesProxied / 1024 / 1024).toFixed(2)}</Statistic.Value>
                    <Statistic.Label>MB Proxied</Statistic.Label>
                  </Statistic>
                </Statistic.Group>
              </Card.Content>
            </Card>
          </Grid.Column>
        )}

        {/* Mesh Benefits */}
        {meshBenefits && (
          <Grid.Column width={8}>
            <Card fluid>
              <Card.Content>
                <Card.Header>
                  <Icon name="sitemap" />
                  Mesh Benefits
                </Card.Header>
              </Card.Content>
              <Card.Content>
                <Statistic.Group size="small">
                  <Statistic>
                    <Statistic.Value>{meshBenefits.meshPercentage.toFixed(1)}%</Statistic.Value>
                    <Statistic.Label>Via Mesh</Statistic.Label>
                  </Statistic>
                  <Statistic>
                    <Statistic.Value>{(meshBenefits.bytesViaMesh / 1024 / 1024).toFixed(2)}</Statistic.Value>
                    <Statistic.Label>MB via Mesh</Statistic.Label>
                  </Statistic>
                </Statistic.Group>
              </Card.Content>
            </Card>
          </Grid.Column>
        )}

        {/* Connected Clients */}
        <Grid.Column width={8}>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="users" />
                Connected Clients ({clients.length})
              </Card.Header>
            </Card.Content>
            <Card.Content>
              {clients.length === 0 ? (
                <Message info>
                  No clients connected
                </Message>
              ) : (
                <Table
                  compact
                  size="small"
                >
                  <Table.Header>
                    <Table.Row>
                      <Table.HeaderCell>Client</Table.HeaderCell>
                      <Table.HeaderCell>IP</Table.HeaderCell>
                      <Table.HeaderCell>Requests</Table.HeaderCell>
                    </Table.Row>
                  </Table.Header>
                  <Table.Body>
                    {clients.map((client) => (
                      <Table.Row key={client.clientId}>
                        <Table.Cell>{client.clientType}</Table.Cell>
                        <Table.Cell>{client.ipAddress}</Table.Cell>
                        <Table.Cell>{client.requestCount}</Table.Cell>
                      </Table.Row>
                    ))}
                  </Table.Body>
                </Table>
              )}
            </Card.Content>
          </Card>
        </Grid.Column>
      </Grid>
    </div>
  );
};

export default Bridge;
