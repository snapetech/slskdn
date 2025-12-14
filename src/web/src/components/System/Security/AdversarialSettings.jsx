import './Security.css';
import * as securityApi from '../../../lib/security';
import React, { useCallback, useEffect, useState } from 'react';
import {
  Button,
  Dimmer,
  Header,
  Icon,
  Loader,
  Message,
  Segment,
  Tab,
  Checkbox,
  Dropdown,
  Input,
  Form,
  Grid,
  Statistic,
  Label,
  TextArea,
} from 'semantic-ui-react';

const AdversarialSettings = () => {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);
  const [settings, setSettings] = useState(null);
  const [hasChanges, setHasChanges] = useState(false);
  const [status, setStatus] = useState(null);
  const [statusLoading, setStatusLoading] = useState(false);
  const [transportStatus, setTransportStatus] = useState(null);
  const [transportLoading, setTransportLoading] = useState(false);
  const [torStatus, setTorStatus] = useState(null);
  const [torLoading, setTorLoading] = useState(false);

  const fetchSettings = useCallback(async () => {
    try {
      setLoading(true);
      const data = await securityApi.getAdversarialSettings().catch(() => null);
      if (data) {
        setSettings(data);
        setError(null);
      } else {
        setError('Adversarial features are not configured on this server');
      }
    } catch (fetchError) {
      setError(fetchError.message || 'Failed to load adversarial settings');
    } finally {
      setLoading(false);
    }
  }, []);

  const fetchStatus = useCallback(async () => {
    try {
      setStatusLoading(true);
      const statusData = await securityApi.getAdversarialStats().catch(() => null);
      if (statusData) {
        setStatus(statusData);
      }
    } catch (statusError) {
      console.error('Failed to load adversarial status:', statusError);
    } finally {
      setStatusLoading(false);
    }
  }, []);

  const fetchTransportStatus = useCallback(async () => {
    try {
      setTransportLoading(true);
      const transportData = await securityApi.getTransportStatus().catch(() => null);
      if (transportData) {
        setTransportStatus(transportData);
      }
    } catch (transportError) {
      console.error('Failed to load transport status:', transportError);
    } finally {
      setTransportLoading(false);
    }
  }, []);

  const fetchTorStatus = useCallback(async () => {
    try {
      setTorLoading(true);
      const torData = await securityApi.getTorStatus().catch(() => null);
      if (torData) {
        setTorStatus(torData);
      }
    } catch (torError) {
      console.error('Failed to load Tor status:', torError);
    } finally {
      setTorLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchSettings();
    fetchStatus();
    fetchTransportStatus();
    fetchTorStatus();
  }, [fetchSettings, fetchStatus, fetchTransportStatus, fetchTorStatus]);

  const handleSave = async () => {
    if (!settings) return;

    try {
      setSaving(true);
      setError(null);
      setSuccess(null);

      await securityApi.updateAdversarialSettings(settings);
      setSuccess('Adversarial settings updated successfully');
      setHasChanges(false);
    } catch (saveError) {
      setError(saveError.message || 'Failed to save adversarial settings');
    } finally {
      setSaving(false);
    }
  };

  const updateSetting = (path, value) => {
    setSettings(prev => {
      const newSettings = { ...prev };
      const keys = path.split('.');
      let current = newSettings;

      for (let i = 0; i < keys.length - 1; i++) {
        if (!current[keys[i]]) current[keys[i]] = {};
        current = current[keys[i]];
      }

      current[keys[keys.length - 1]] = value;
      return newSettings;
    });
    setHasChanges(true);
  };

  const updateArray = (path, index, value) => {
    setSettings(prev => {
      const newSettings = { ...prev };
      const keys = path.split('.');
      let current = newSettings;

      for (let i = 0; i < keys.length - 1; i++) {
        if (!current[keys[i]]) current[keys[i]] = {};
        current = current[keys[i]];
      }

      if (!Array.isArray(current[keys[keys.length - 1]])) {
        current[keys[keys.length - 1]] = [];
      }

      current[keys[keys.length - 1]][index] = value;
      return newSettings;
    });
    setHasChanges(true);
  };

  const addArrayItem = (path) => {
    setSettings(prev => {
      const newSettings = { ...prev };
      const keys = path.split('.');
      let current = newSettings;

      for (let i = 0; i < keys.length - 1; i++) {
        if (!current[keys[i]]) current[keys[i]] = {};
        current = current[keys[i]];
      }

      if (!Array.isArray(current[keys[keys.length - 1]])) {
        current[keys[keys.length - 1]] = [];
      }

      current[keys[keys.length - 1]].push('');
      return newSettings;
    });
    setHasChanges(true);
  };

  const removeArrayItem = (path, index) => {
    setSettings(prev => {
      const newSettings = { ...prev };
      const keys = path.split('.');
      let current = newSettings;

      for (let i = 0; i < keys.length - 1; i++) {
        if (!current[keys[i]]) current[keys[i]] = {};
        current = current[keys[i]];
      }

      if (Array.isArray(current[keys[keys.length - 1]])) {
        current[keys[keys.length - 1]].splice(index, 1);
      }

      return newSettings;
    });
    setHasChanges(true);
  };

  if (loading) {
    return (
      <Segment placeholder>
        <Dimmer active inverted>
          <Loader>Loading Adversarial Settings...</Loader>
        </Dimmer>
      </Segment>
    );
  }

  if (error && !settings) {
    return (
      <Message negative>
        <Message.Header>Adversarial Features Unavailable</Message.Header>
        <p>{error}</p>
        <p>
          Adversarial features are advanced security options designed for users in adversarial environments.
          They are disabled by default and require explicit configuration.
        </p>
        <Button onClick={fetchSettings} size="small">
          Retry
        </Button>
      </Message>
    );
  }

  if (!settings) {
    return (
      <Message info>
        <Message.Header>Adversarial Settings Not Configured</Message.Header>
        <p>No adversarial configuration found.</p>
      </Message>
    );
  }

  const profileOptions = [
    { key: 'disabled', text: 'Disabled', value: 'Disabled' },
    { key: 'standard', text: 'Standard (Privacy)', value: 'Standard' },
    { key: 'enhanced', text: 'Enhanced (Privacy + Anonymity)', value: 'Enhanced' },
    { key: 'maximum', text: 'Maximum (All Features)', value: 'Maximum' },
    { key: 'custom', text: 'Custom', value: 'Custom' },
  ];

  const transportOptions = [
    { key: 'Direct', text: 'Direct', value: 'Direct' },
    { key: 'WebSocket', text: 'WebSocket', value: 'WebSocket' },
    { key: 'HttpTunnel', text: 'HTTP Tunnel', value: 'HttpTunnel' },
    { key: 'Obfs4', text: 'Obfs4', value: 'Obfs4' },
    { key: 'Meek', text: 'Meek', value: 'Meek' },
  ];

  const anonymityModeOptions = [
    { key: 'Direct', text: 'Direct', value: 'Direct' },
    { key: 'Tor', text: 'Tor', value: 'Tor' },
    { key: 'I2P', text: 'I2P', value: 'I2P' },
    { key: 'RelayOnly', text: 'Relay Only', value: 'RelayOnly' },
  ];

  const panes = [
    {
      menuItem: 'Overview',
      render: () => (
        <Tab.Pane>
          <Header as="h3">
            <Icon name="shield alternate" />
            Adversarial Resilience Overview
          </Header>
          <p>
            <strong>⚠️ WARNING:</strong> These features are designed for users in repressive regimes
            or facing active surveillance. They are <strong>ALL DISABLED BY DEFAULT</strong> and may
            impact performance and compatibility. Only enable if you understand the security implications.
          </p>

          <Form>
            <Form.Field>
              <label>Adversarial Profile</label>
              <Dropdown
                selection
                options={profileOptions}
                value={settings.Profile || 'Disabled'}
                onChange={(e, { value }) => updateSetting('Profile', value)}
              />
            </Form.Field>

            <Form.Field>
              <Checkbox
                label="Enable Adversarial Features"
                checked={settings.Enabled || false}
                onChange={(e, { checked }) => updateSetting('Enabled', checked)}
              />
            </Form.Field>
          </Form>

          {settings.Enabled && (
            <>
              <Message info>
                <Message.Header>Active Features</Message.Header>
                <ul>
                  {settings.Privacy?.Enabled && <li>Privacy Layer (Traffic Analysis Protection)</li>}
                  {settings.Anonymity?.Enabled && <li>Anonymity Layer (IP Protection)</li>}
                  {settings.Transport?.Enabled && <li>Obfuscated Transport (Anti-DPI)</li>}
                  {settings.OnionRouting?.Enabled && <li>Onion Routing (Mesh Anonymity)</li>}
                  {settings.CensorshipResistance?.Enabled && <li>Censorship Resistance</li>}
                  {settings.PlausibleDeniability?.Enabled && <li>Plausible Deniability</li>}
                </ul>
              </Message>

              <Segment>
                <Header as="h4">
                  <Icon name="signal" />
                  Transport Status
                </Header>
                <Grid columns={3} stackable>
                  <Grid.Column>
                    <Statistic size="small">
                      <Statistic.Value>
                        {statusLoading ? (
                          <Loader active inline size="mini" />
                        ) : status?.AnonymityEnabled ? (
                          <Label color="green">
                            <Icon name="check" />
                            Enabled
                          </Label>
                        ) : (
                          <Label color="grey">
                            <Icon name="minus" />
                            Disabled
                          </Label>
                        )}
                      </Statistic.Value>
                      <Statistic.Label>Anonymity Layer</Statistic.Label>
                    </Statistic>
                  </Grid.Column>
                  <Grid.Column>
                    <Statistic size="small">
                      <Statistic.Value>
                        {statusLoading ? (
                          <Loader active inline size="mini" />
                        ) : settings.Anonymity?.Mode === 'Tor' ? (
                          <Label color="orange">
                            <Icon name="shield" />
                            Tor
                          </Label>
                        ) : settings.Anonymity?.Mode === 'I2P' ? (
                          <Label color="purple">
                            <Icon name="privacy" />
                            I2P
                          </Label>
                        ) : settings.Anonymity?.Mode === 'RelayOnly' ? (
                          <Label color="blue">
                            <Icon name="chain" />
                            Relay Only
                          </Label>
                        ) : (
                          <Label color="grey">
                            <Icon name="globe" />
                            Direct
                          </Label>
                        )}
                      </Statistic.Value>
                      <Statistic.Label>Transport Mode</Statistic.Label>
                    </Statistic>
                  </Grid.Column>
                  <Grid.Column>
                    <Statistic size="small">
                      <Statistic.Value>
                        <Label color="teal">
                          <Icon name="sync alternate" />
                          Auto
                        </Label>
                      </Statistic.Value>
                      <Statistic.Label>Failover</Statistic.Label>
                    </Statistic>
                  </Grid.Column>
                  <Grid.Column>
                    <Statistic size="small">
                      <Statistic.Value>
                        {transportLoading ? (
                          <Loader active inline size="mini" />
                        ) : transportStatus ? (
                          <Label color={transportStatus.PrimaryTransportAvailable ? "green" : "red"}>
                            <Icon name="shield" />
                            {transportStatus.AvailableTransports}/{transportStatus.TotalTransports}
                          </Label>
                        ) : (
                          <Label color="grey">
                            <Icon name="question circle" />
                            N/A
                          </Label>
                        )}
                      </Statistic.Value>
                      <Statistic.Label>Transports Up</Statistic.Label>
                    </Statistic>
                  </Grid.Column>
                  <Grid.Column>
                    <Statistic size="small">
                      <Statistic.Value>
                        {torLoading ? (
                          <Loader active inline size="mini" />
                        ) : torStatus ? (
                          <Label color={torStatus.IsAvailable ? "green" : "red"}>
                            <Icon name="shield alternate" />
                            {torStatus.IsAvailable ? "Connected" : "Disconnected"}
                          </Label>
                        ) : (
                          <Label color="grey">
                            <Icon name="question circle" />
                            N/A
                          </Label>
                        )}
                      </Statistic.Value>
                      <Statistic.Label>Tor Status</Statistic.Label>
                    </Statistic>
                  </Grid.Column>
                </Grid>

                {settings.Anonymity?.Mode === 'Tor' && (
                  <Message info>
                    <Message.Header>Tor Configuration</Message.Header>
                    <p><strong>SOCKS Address:</strong> {settings.Anonymity.Tor?.SocksAddress || '127.0.0.1:9050'}</p>
                    <p><strong>Stream Isolation:</strong> {settings.Anonymity.Tor?.IsolateStreams ? 'Enabled' : 'Disabled'}</p>
                    <p><em>Stream isolation prevents correlation attacks by using different Tor circuits per peer.</em></p>
                  </Message>
                )}

                {settings.Anonymity?.Mode === 'Tor' && torStatus && (
                  <Message color={torStatus.IsAvailable ? "green" : "red"}>
                    <Message.Header>Tor Status</Message.Header>
                    <p><strong>Status:</strong> {torStatus.IsAvailable ? 'Connected' : 'Disconnected'}</p>
                    {torStatus.LastError && <p><strong>Last Error:</strong> {torStatus.LastError}</p>}
                    {torStatus.LastSuccessfulConnection && (
                      <p><strong>Last Connected:</strong> {new Date(torStatus.LastSuccessfulConnection).toLocaleString()}</p>
                    )}
                    <p><strong>Active Connections:</strong> {torStatus.ActiveConnections}</p>
                    <p><strong>Total Attempts:</strong> {torStatus.TotalConnectionsAttempted}</p>
                    <p><strong>Successful Connections:</strong> {torStatus.TotalConnectionsSuccessful}</p>
                  </Message>
                )}

                {settings.Anonymity?.Mode === 'I2P' && (
                  <Message info>
                    <Message.Header>I2P Configuration</Message.Header>
                    <p><strong>SAM Address:</strong> {settings.Anonymity.I2P?.SamAddress || '127.0.0.1:7656'}</p>
                    <p><em>I2P provides peer-to-peer anonymity with better performance for persistent connections.</em></p>
                  </Message>
                )}

                {settings.Anonymity?.Mode === 'RelayOnly' && (
                  <Message info>
                    <Message.Header>Relay-Only Configuration</Message.Header>
                    <p><strong>Trusted Relays:</strong> {settings.Anonymity.RelayOnly?.TrustedRelayPeers?.length || 0} configured</p>
                    <p><strong>Max Chain Length:</strong> {settings.Anonymity.RelayOnly?.MaxChainLength || 3}</p>
                    <p><em>Never reveals your IP address - all connections route through trusted mesh relays.</em></p>
                  </Message>
                )}
              </Segment>
            </>
          )}
        </Tab.Pane>
      ),
    },
    {
      menuItem: 'Privacy',
      render: () => (
        <Tab.Pane>
          <Header as="h4">Privacy Layer - Traffic Analysis Protection</Header>
          <p>Protect against traffic analysis by modifying message timing and size patterns.</p>

          <Form>
            <Form.Field>
              <Checkbox
                label="Enable Privacy Layer"
                checked={settings.Privacy?.Enabled || false}
                onChange={(e, { checked }) => updateSetting('Privacy.Enabled', checked)}
              />
            </Form.Field>

            {settings.Privacy?.Enabled && (
              <>
                <Header as="h5">Message Padding</Header>
                <Form.Field>
                  <Checkbox
                    label="Enable Message Padding"
                    checked={settings.Privacy.Padding?.Enabled || false}
                    onChange={(e, { checked }) => updateSetting('Privacy.Padding.Enabled', checked)}
                  />
                </Form.Field>

                {settings.Privacy.Padding?.Enabled && (
                  <>
                    <Form.Field>
                      <label>Bucket Sizes (bytes)</label>
                      {(settings.Privacy.Padding?.BucketSizes || []).map((size, index) => (
                        <Input
                          key={index}
                          type="number"
                          value={size}
                          onChange={(e) => updateArray('Privacy.Padding.BucketSizes', index, parseInt(e.target.value))}
                          style={{ marginBottom: '5px' }}
                        />
                      ))}
                      <Button
                        icon="plus"
                        size="mini"
                        onClick={() => addArrayItem('Privacy.Padding.BucketSizes')}
                      />
                    </Form.Field>

                    <Form.Field>
                      <Checkbox
                        label="Use Random Fill Bytes"
                        checked={settings.Privacy.Padding?.UseRandomFill || false}
                        onChange={(e, { checked }) => updateSetting('Privacy.Padding.UseRandomFill', checked)}
                      />
                    </Form.Field>
                  </>
                )}

                <Header as="h5">Timing Obfuscation</Header>
                <Form.Field>
                  <Checkbox
                    label="Enable Timing Obfuscation"
                    checked={settings.Privacy.Timing?.Enabled || false}
                    onChange={(e, { checked }) => updateSetting('Privacy.Timing.Enabled', checked)}
                  />
                </Form.Field>

                {settings.Privacy.Timing?.Enabled && (
                  <Form.Field>
                    <label>Jitter Range (ms)</label>
                    <Input
                      type="number"
                      min="0"
                      max="500"
                      value={settings.Privacy.Timing?.JitterMs || 100}
                      onChange={(e) => updateSetting('Privacy.Timing.JitterMs', parseInt(e.target.value))}
                    />
                  </Form.Field>
                )}

                <Header as="h5">Message Batching</Header>
                <Form.Field>
                  <Checkbox
                    label="Enable Message Batching"
                    checked={settings.Privacy.Batching?.Enabled || false}
                    onChange={(e, { checked }) => updateSetting('Privacy.Batching.Enabled', checked)}
                  />
                </Form.Field>

                {settings.Privacy.Batching?.Enabled && (
                  <Form.Field>
                    <label>Batch Window (ms)</label>
                    <Input
                      type="number"
                      min="100"
                      max="5000"
                      value={settings.Privacy.Batching?.BatchWindowMs || 1000}
                      onChange={(e) => updateSetting('Privacy.Batching.BatchWindowMs', parseInt(e.target.value))}
                    />
                  </Form.Field>
                )}

                <Header as="h5">Cover Traffic</Header>
                <Form.Field>
                  <Checkbox
                    label="Enable Cover Traffic"
                    checked={settings.Privacy.CoverTraffic?.Enabled || false}
                    onChange={(e, { checked }) => updateSetting('Privacy.CoverTraffic.Enabled', checked)}
                  />
                </Form.Field>

                {settings.Privacy.CoverTraffic?.Enabled && (
                  <Form.Field>
                    <label>Interval (seconds)</label>
                    <Input
                      type="number"
                      min="10"
                      max="3600"
                      value={settings.Privacy.CoverTraffic?.IntervalSeconds || 300}
                      onChange={(e) => updateSetting('Privacy.CoverTraffic.IntervalSeconds', parseInt(e.target.value))}
                    />
                  </Form.Field>
                )}
              </>
            )}
          </Form>
        </Tab.Pane>
      ),
    },
    {
      menuItem: 'Anonymity',
      render: () => (
        <Tab.Pane>
          <Header as="h4">Anonymity Layer - IP Protection</Header>
          <p>Route traffic through anonymizing networks to hide your IP address.</p>

          <Form>
            <Form.Field>
              <Checkbox
                label="Enable Anonymity Layer"
                checked={settings.Anonymity?.Enabled || false}
                onChange={(e, { checked }) => updateSetting('Anonymity.Enabled', checked)}
              />
            </Form.Field>

            {settings.Anonymity?.Enabled && (
              <>
                <Form.Field>
                  <label>Anonymity Mode</label>
                  <Dropdown
                    selection
                    options={anonymityModeOptions}
                    value={settings.Anonymity?.Mode || 'Direct'}
                    onChange={(e, { value }) => updateSetting('Anonymity.Mode', value)}
                  />
                </Form.Field>

                {settings.Anonymity?.Mode === 'Tor' && (
                  <>
                    <Header as="h5">Tor Configuration</Header>
                    <Form.Field>
                      <label>SOCKS Address</label>
                      <Input
                        value={settings.Anonymity.Tor?.SocksAddress || '127.0.0.1:9050'}
                        onChange={(e) => updateSetting('Anonymity.Tor.SocksAddress', e.target.value)}
                      />
                    </Form.Field>

                    <Form.Field>
                      <Checkbox
                        label="Isolate Streams Per Peer"
                        checked={settings.Anonymity.Tor?.IsolateStreams || false}
                        onChange={(e, { checked }) => updateSetting('Anonymity.Tor.IsolateStreams', checked)}
                      />
                    </Form.Field>
                  </>
                )}

                {settings.Anonymity?.Mode === 'I2P' && (
                  <>
                    <Header as="h5">I2P Configuration</Header>
                    <Form.Field>
                      <label>SAM Address</label>
                      <Input
                        value={settings.Anonymity.I2P?.SamAddress || '127.0.0.1:7656'}
                        onChange={(e) => updateSetting('Anonymity.I2P.SamAddress', e.target.value)}
                      />
                    </Form.Field>
                  </>
                )}

                {settings.Anonymity?.Mode === 'RelayOnly' && (
                  <>
                    <Header as="h5">Relay Configuration</Header>
                    <Form.Field>
                      <label>Max Chain Length</label>
                      <Input
                        type="number"
                        min="1"
                        max="5"
                        value={settings.Anonymity.RelayOnly?.MaxChainLength || 3}
                        onChange={(e) => updateSetting('Anonymity.RelayOnly.MaxChainLength', parseInt(e.target.value))}
                      />
                    </Form.Field>
                  </>
                )}
              </>
            )}
          </Form>
        </Tab.Pane>
      ),
    },
    {
      menuItem: 'Transport',
      render: () => (
        <Tab.Pane>
          <Header as="h4">Obfuscated Transport - Anti-DPI</Header>
          <p>Use obfuscated protocols to bypass deep packet inspection.</p>

          <Form>
            <Form.Field>
              <Checkbox
                label="Enable Obfuscated Transport"
                checked={settings.Transport?.Enabled || false}
                onChange={(e, { checked }) => updateSetting('Transport.Enabled', checked)}
              />
            </Form.Field>

            {settings.Transport?.Enabled && (
              <>
                <Form.Field>
                  <label>Primary Transport</label>
                  <Dropdown
                    selection
                    options={transportOptions}
                    value={settings.Transport?.PrimaryTransport || 'Direct'}
                    onChange={(e, { value }) => updateSetting('Transport.PrimaryTransport', value)}
                  />
                </Form.Field>

                {settings.Transport?.PrimaryTransport === 'WebSocket' && (
                  <>
                    <Header as="h5">WebSocket Configuration</Header>
                    <Form.Field>
                      <label>Server URL</label>
                      <Input
                        placeholder="wss://websocket-server.example.com/tunnel"
                        value={settings.Transport.WebSocket?.ServerUrl || ''}
                        onChange={(e) => updateSetting('Transport.WebSocket.ServerUrl', e.target.value)}
                      />
                      <small style={{ color: '#666', marginTop: '5px', display: 'block' }}>
                        WebSocket server that will proxy connections. Traffic appears as normal web traffic to DPI systems.
                      </small>
                    </Form.Field>

                    <Form.Field>
                      <Checkbox
                        label="Use WSS (Secure WebSocket)"
                        checked={settings.Transport.WebSocket?.UseWss || false}
                        onChange={(e, { checked }) => updateSetting('Transport.WebSocket.UseWss', checked)}
                      />
                    </Form.Field>
                  </>
                )}

                {settings.Transport?.PrimaryTransport === 'HttpTunnel' && (
                  <>
                    <Header as="h5">HTTP Tunnel Configuration</Header>
                    <Form.Field>
                      <label>Proxy URL</label>
                      <Input
                        placeholder="https://http-proxy.example.com/tunnel"
                        value={settings.Transport.HttpTunnel?.ProxyUrl || ''}
                        onChange={(e) => updateSetting('Transport.HttpTunnel.ProxyUrl', e.target.value)}
                      />
                      <small style={{ color: '#666', marginTop: '5px', display: 'block' }}>
                        HTTP proxy server that will tunnel connections. Traffic appears as normal HTTP requests.
                      </small>
                    </Form.Field>

                    <Form.Field>
                      <label>HTTP Method</label>
                      <Dropdown
                        selection
                        options={[
                          { key: 'POST', text: 'POST', value: 'POST' },
                          { key: 'GET', text: 'GET', value: 'GET' },
                          { key: 'PUT', text: 'PUT', value: 'PUT' },
                        ]}
                        value={settings.Transport.HttpTunnel?.Method || 'POST'}
                        onChange={(e, { value }) => updateSetting('Transport.HttpTunnel.Method', value)}
                      />
                    </Form.Field>

                    <Form.Field>
                      <Checkbox
                        label="Use HTTPS"
                        checked={settings.Transport.HttpTunnel?.UseHttps || false}
                        onChange={(e, { checked }) => updateSetting('Transport.HttpTunnel.UseHttps', checked)}
                      />
                    </Form.Field>
                  </>
                )}

                {settings.Transport?.PrimaryTransport === 'Obfs4' && (
                  <>
                    <Header as="h5">Obfs4 Configuration</Header>
                    <Form.Field>
                      <label>Obfs4 Proxy Path</label>
                      <Input
                        placeholder="/usr/bin/obfs4proxy"
                        value={settings.Transport.Obfs4?.Obfs4ProxyPath || ''}
                        onChange={(e) => updateSetting('Transport.Obfs4.Obfs4ProxyPath', e.target.value)}
                      />
                    </Form.Field>

                    <Form.Field>
                      <label>Bridge Lines</label>
                      <small style={{ color: '#666', marginBottom: '10px', display: 'block' }}>
                        Tor bridge lines for Obfs4 bridges (one per line)
                      </small>
                      <TextArea
                        placeholder="obfs4 192.0.2.1:443 1234567890ABCDEF..."
                        value={(settings.Transport.Obfs4?.BridgeLines || []).join('\n')}
                        onChange={(e) => {
                          const lines = e.target.value.split('\n').filter(line => line.trim());
                          updateSetting('Transport.Obfs4.BridgeLines', lines);
                        }}
                        rows={4}
                      />
                    </Form.Field>
                  </>
                )}

                {settings.Transport?.PrimaryTransport === 'Obfs4' && (
                  <>
                    <Header as="h5">Obfs4 Configuration</Header>
                    <Form.Field>
                      <label>Bridge Lines</label>
                      {(settings.Transport.Obfs4?.BridgeLines || []).map((line, index) => (
                        <div key={index} style={{ marginBottom: '5px' }}>
                          <Input
                            value={line}
                            onChange={(e) => updateArray('Transport.Obfs4.BridgeLines', index, e.target.value)}
                            style={{ marginRight: '5px' }}
                          />
                          <Button
                            icon="minus"
                            size="mini"
                            onClick={() => removeArrayItem('Transport.Obfs4.BridgeLines', index)}
                          />
                        </div>
                      ))}
                      <Button
                        icon="plus"
                        size="mini"
                        onClick={() => addArrayItem('Transport.Obfs4.BridgeLines')}
                      />
                    </Form.Field>
                  </>
                )}

                {settings.Transport?.PrimaryTransport === 'Meek' && (
                  <>
                    <Header as="h5">Meek Configuration</Header>
                    <Form.Field>
                      <label>Bridge URL</label>
                      <Input
                        placeholder="https://meek-bridge.example.com/connect"
                        value={settings.Transport?.Meek?.BridgeUrl || ''}
                        onChange={(e) => updateSetting('Transport.Meek.BridgeUrl', e.target.value)}
                      />
                      <small style={{ color: '#666', marginTop: '5px', display: 'block' }}>
                        Meek bridge server URL that will proxy connections through domain fronting.
                      </small>
                    </Form.Field>

                    <Form.Field>
                      <label>Front Domain</label>
                      <Input
                        placeholder="www.google.com"
                        value={settings.Transport?.Meek?.FrontDomain || ''}
                        onChange={(e) => updateSetting('Transport.Meek.FrontDomain', e.target.value)}
                      />
                      <small style={{ color: '#666', marginTop: '5px', display: 'block' }}>
                        Domain to front through (e.g., major CDN domains). Traffic appears to connect to this domain.
                      </small>
                    </Form.Field>
                  </>
                )}
              </>
            )}
          </Form>
        </Tab.Pane>
      ),
    },
  ];

  return (
    <div className="adversarial-settings">
      <div className="security-header">
        <Header as="h3">
          <Icon name="user secret" />
          <Header.Content>
            Adversarial Settings
            <Header.Subheader>Advanced privacy and anonymity features</Header.Subheader>
          </Header.Content>
        </Header>
        <div>
          <Button
            icon="refresh"
            onClick={() => { fetchSettings(); fetchStatus(); fetchTransportStatus(); fetchTorStatus(); }}
            size="tiny"
            title="Refresh Settings & Status"
          />
          <Button
            icon="plug"
            onClick={async () => {
              try {
                await securityApi.testTransportConnectivity();
                setSuccess('Transport connectivity test completed');
                fetchTransportStatus();
              } catch (error) {
                setError(error?.response?.data ?? error?.message ?? 'Transport test failed');
              }
            }}
            size="tiny"
            title="Test Transport Connectivity"
            loading={transportLoading}
          />
          <Button
            icon="shield alternate"
            onClick={async () => {
              try {
                await securityApi.testTorConnectivity();
                setSuccess('Tor connectivity test completed');
                fetchTorStatus();
              } catch (error) {
                setError(error?.response?.data ?? error?.message ?? 'Tor test failed');
              }
            }}
            size="tiny"
            title="Test Tor Connectivity"
            loading={torLoading}
          />
          <Button
            primary
            icon="save"
            loading={saving}
            disabled={!hasChanges}
            onClick={handleSave}
            style={{ marginLeft: '10px' }}
          >
            Save Changes
          </Button>
        </div>
      </div>

      {error && (
        <Message negative>
          <Message.Header>Error</Message.Header>
          <p>{error}</p>
        </Message>
      )}

      {success && (
        <Message positive>
          <Message.Header>Success</Message.Header>
          <p>{success}</p>
        </Message>
      )}

      <Tab panes={panes} />
    </div>
  );
};

export default AdversarialSettings;
