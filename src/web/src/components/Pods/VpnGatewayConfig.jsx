import React, { useState, useEffect } from 'react';
import {
  Button,
  Form,
  Header,
  Icon,
  Message,
  Modal,
  Segment,
  Tab,
  Table,
  Label,
  Input,
  Dropdown,
  Checkbox,
} from 'semantic-ui-react';
import * as pods from '../../lib/pods';

const VpnGatewayConfig = ({ podId, podDetail }) => {
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);

  // VPN Policy state
  const [vpnPolicy, setVpnPolicy] = useState({
    enabled: false,
    maxMembers: 3,
    gatewayPeerId: '',
    allowPrivateRanges: true,
    allowPublicDestinations: false,
    allowedDestinations: [],
    registeredServices: [],
    maxConcurrentTunnelsPerPeer: 5,
    maxConcurrentTunnelsPod: 15,
    maxNewTunnelsPerMinutePerPeer: 10,
    maxBytesPerDayPerPeer: 1073741824, // 1GB
    idleTimeout: '01:00:00',
    maxLifetime: '24:00:00',
    dialTimeout: '00:00:30'
  });

  // Modal states for adding destinations and services
  const [showAddDestination, setShowAddDestination] = useState(false);
  const [showAddService, setShowAddService] = useState(false);
  const [newDestination, setNewDestination] = useState({
    hostPattern: '',
    port: '',
    protocol: 'tcp'
  });
  const [newService, setNewService] = useState({
    name: '',
    description: '',
    kind: 'WebInterface',
    destinationHost: '',
    destinationPort: '',
    protocol: 'tcp'
  });

  useEffect(() => {
    if (podDetail?.privateServicePolicy) {
      setVpnPolicy({
        ...podDetail.privateServicePolicy,
        // Ensure defaults for missing fields
        maxMembers: podDetail.privateServicePolicy.maxMembers || 3,
        gatewayPeerId: podDetail.privateServicePolicy.gatewayPeerId || '',
        allowPrivateRanges: podDetail.privateServicePolicy.allowPrivateRanges ?? true,
        allowPublicDestinations: podDetail.privateServicePolicy.allowPublicDestinations ?? false,
        allowedDestinations: podDetail.privateServicePolicy.allowedDestinations || [],
        registeredServices: podDetail.privateServicePolicy.registeredServices || [],
        maxConcurrentTunnelsPerPeer: podDetail.privateServicePolicy.maxConcurrentTunnelsPerPeer || 5,
        maxConcurrentTunnelsPod: podDetail.privateServicePolicy.maxConcurrentTunnelsPod || 15,
        maxNewTunnelsPerMinutePerPeer: podDetail.privateServicePolicy.maxNewTunnelsPerMinutePerPeer || 10,
        maxBytesPerDayPerPeer: podDetail.privateServicePolicy.maxBytesPerDayPerPeer || 1073741824,
        idleTimeout: podDetail.privateServicePolicy.idleTimeout || '01:00:00',
        maxLifetime: podDetail.privateServicePolicy.maxLifetime || '24:00:00',
        dialTimeout: podDetail.privateServicePolicy.dialTimeout || '00:00:30'
      });
    }
  }, [podDetail]);

  const hasVpnCapability = podDetail?.capabilities?.includes('PrivateServiceGateway');

  const handleSavePolicy = async () => {
    if (!podId) return;

    setSaving(true);
    setError(null);
    setSuccess(null);

    try {
      // Create updated pod with VPN policy
      const updatedPod = {
        ...podDetail,
        privateServicePolicy: vpnPolicy.enabled ? vpnPolicy : null
      };

      await pods.update(podId, updatedPod);
      setSuccess('VPN policy updated successfully');
    } catch (error) {
      console.error('Failed to update VPN policy:', error);
      setError(error.message || 'Failed to update VPN policy');
    } finally {
      setSaving(false);
    }
  };

  const handleAddDestination = () => {
    if (!newDestination.hostPattern || !newDestination.port) return;

    const updatedDestinations = [
      ...vpnPolicy.allowedDestinations,
      {
        hostPattern: newDestination.hostPattern,
        port: parseInt(newDestination.port, 10),
        protocol: newDestination.protocol
      }
    ];

    setVpnPolicy(prev => ({ ...prev, allowedDestinations: updatedDestinations }));
    setNewDestination({ hostPattern: '', port: '', protocol: 'tcp' });
    setShowAddDestination(false);
  };

  const handleRemoveDestination = (index) => {
    const updatedDestinations = vpnPolicy.allowedDestinations.filter((_, i) => i !== index);
    setVpnPolicy(prev => ({ ...prev, allowedDestinations: updatedDestinations }));
  };

  const handleAddService = () => {
    if (!newService.name || !newService.destinationHost || !newService.destinationPort) return;

    const updatedServices = [
      ...vpnPolicy.registeredServices,
      {
        name: newService.name,
        description: newService.description,
        kind: newService.kind,
        destinationHost: newService.destinationHost,
        destinationPort: parseInt(newService.destinationPort, 10),
        protocol: newService.protocol
      }
    ];

    setVpnPolicy(prev => ({ ...prev, registeredServices: updatedServices }));
    setNewService({
      name: '',
      description: '',
      kind: 'WebInterface',
      destinationHost: '',
      destinationPort: '',
      protocol: 'tcp'
    });
    setShowAddService(false);
  };

  const handleRemoveService = (index) => {
    const updatedServices = vpnPolicy.registeredServices.filter((_, i) => i !== index);
    setVpnPolicy(prev => ({ ...prev, registeredServices: updatedServices }));
  };

  const formatBytes = (bytes) => {
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let value = bytes;
    let unitIndex = 0;

    while (value >= 1024 && unitIndex < units.length - 1) {
      value /= 1024;
      unitIndex++;
    }

    return `${value.toFixed(1)} ${units[unitIndex]}`;
  };

  if (!hasVpnCapability) {
    return (
      <Segment placeholder>
        <Header icon>
          <Icon name="lock" />
          VPN Gateway Not Enabled
        </Header>
        <Segment.Inline>
          <p>This pod does not have VPN gateway capability enabled.</p>
          <p>To enable VPN functionality, add the "PrivateServiceGateway" capability to the pod.</p>
        </Segment.Inline>
      </Segment>
    );
  }

  const serviceKindOptions = [
    { key: 'WebInterface', text: 'Web Interface', value: 'WebInterface' },
    { key: 'Database', text: 'Database', value: 'Database' },
    { key: 'SSH', text: 'SSH', value: 'SSH' },
    { key: 'Custom', text: 'Custom', value: 'Custom' }
  ];

  const protocolOptions = [
    { key: 'tcp', text: 'TCP', value: 'tcp' },
    { key: 'udp', text: 'UDP', value: 'udp' }
  ];

  const panes = [
    {
      menuItem: 'Basic Settings',
      render: () => (
        <Tab.Pane>
          <Form>
            <Form.Group>
              <Form.Field width={4}>
                <label>Enable VPN Gateway</label>
                <Checkbox
                  toggle
                  checked={vpnPolicy.enabled}
                  onChange={(e, { checked }) => setVpnPolicy(prev => ({ ...prev, enabled: checked }))}
                />
              </Form.Field>
              <Form.Field width={4}>
                <label>Max Pod Members</label>
                <Input
                  type="number"
                  min={1}
                  max={3}
                  value={vpnPolicy.maxMembers}
                  onChange={(e, { value }) => setVpnPolicy(prev => ({ ...prev, maxMembers: parseInt(value, 10) || 3 }))}
                  disabled={!vpnPolicy.enabled}
                />
                <small>Hard limit of 3 for VPN-enabled pods</small>
              </Form.Field>
              <Form.Field width={8}>
                <label>Gateway Peer ID</label>
                <Input
                  placeholder="peer-id-of-gateway-node"
                  value={vpnPolicy.gatewayPeerId}
                  onChange={(e, { value }) => setVpnPolicy(prev => ({ ...prev, gatewayPeerId: value }))}
                  disabled={!vpnPolicy.enabled}
                />
              </Form.Field>
            </Form.Group>

            <Header as="h4">Network Access Control</Header>
            <Form.Group>
              <Form.Field>
                <Checkbox
                  label="Allow private IP ranges (RFC1918)"
                  checked={vpnPolicy.allowPrivateRanges}
                  onChange={(e, { checked }) => setVpnPolicy(prev => ({ ...prev, allowPrivateRanges: checked }))}
                  disabled={!vpnPolicy.enabled}
                />
              </Form.Field>
              <Form.Field>
                <Checkbox
                  label="Allow public internet destinations"
                  checked={vpnPolicy.allowPublicDestinations}
                  onChange={(e, { checked }) => setVpnPolicy(prev => ({ ...prev, allowPublicDestinations: checked }))}
                  disabled={!vpnPolicy.enabled}
                />
              </Form.Field>
            </Form.Group>
          </Form>
        </Tab.Pane>
      ),
    },
    {
      menuItem: 'Allowed Destinations',
      render: () => (
        <Tab.Pane>
          <div style={{ marginBottom: '20px' }}>
            <Button
              primary
              icon="plus"
              content="Add Destination"
              onClick={() => setShowAddDestination(true)}
              disabled={!vpnPolicy.enabled}
            />
          </div>

          <Table celled>
            <Table.Header>
              <Table.Row>
                <Table.HeaderCell>Host Pattern</Table.HeaderCell>
                <Table.HeaderCell>Port</Table.HeaderCell>
                <Table.HeaderCell>Protocol</Table.HeaderCell>
                <Table.HeaderCell>Actions</Table.HeaderCell>
              </Table.Row>
            </Table.Header>
            <Table.Body>
              {vpnPolicy.allowedDestinations.map((dest, index) => (
                <Table.Row key={index}>
                  <Table.Cell>{dest.hostPattern}</Table.Cell>
                  <Table.Cell>{dest.port}</Table.Cell>
                  <Table.Cell>{dest.protocol?.toUpperCase()}</Table.Cell>
                  <Table.Cell>
                    <Button
                      icon="trash"
                      color="red"
                      size="small"
                      onClick={() => handleRemoveDestination(index)}
                      disabled={!vpnPolicy.enabled}
                    />
                  </Table.Cell>
                </Table.Row>
              ))}
              {vpnPolicy.allowedDestinations.length === 0 && (
                <Table.Row>
                  <Table.Cell colSpan={4} textAlign="center">
                    No destinations configured
                  </Table.Cell>
                </Table.Row>
              )}
            </Table.Body>
          </Table>
        </Tab.Pane>
      ),
    },
    {
      menuItem: 'Registered Services',
      render: () => (
        <Tab.Pane>
          <div style={{ marginBottom: '20px' }}>
            <Button
              primary
              icon="plus"
              content="Add Service"
              onClick={() => setShowAddService(true)}
              disabled={!vpnPolicy.enabled}
            />
          </div>

          <Table celled>
            <Table.Header>
              <Table.Row>
                <Table.HeaderCell>Name</Table.HeaderCell>
                <Table.HeaderCell>Description</Table.HeaderCell>
                <Table.HeaderCell>Type</Table.HeaderCell>
                <Table.HeaderCell>Destination</Table.HeaderCell>
                <Table.HeaderCell>Actions</Table.HeaderCell>
              </Table.Row>
            </Table.Header>
            <Table.Body>
              {vpnPolicy.registeredServices.map((service, index) => (
                <Table.Row key={index}>
                  <Table.Cell>{service.name}</Table.Cell>
                  <Table.Cell>{service.description}</Table.Cell>
                  <Table.Cell>
                    <Label color="blue">{service.kind}</Label>
                  </Table.Cell>
                  <Table.Cell>
                    {service.destinationHost}:{service.destinationPort} ({service.protocol})
                  </Table.Cell>
                  <Table.Cell>
                    <Button
                      icon="trash"
                      color="red"
                      size="small"
                      onClick={() => handleRemoveService(index)}
                      disabled={!vpnPolicy.enabled}
                    />
                  </Table.Cell>
                </Table.Row>
              ))}
              {vpnPolicy.registeredServices.length === 0 && (
                <Table.Row>
                  <Table.Cell colSpan={5} textAlign="center">
                    No services registered
                  </Table.Cell>
                </Table.Row>
              )}
            </Table.Body>
          </Table>
        </Tab.Pane>
      ),
    },
    {
      menuItem: 'Resource Limits',
      render: () => (
        <Tab.Pane>
          <Form>
            <Header as="h4">Connection Limits</Header>
            <Form.Group widths="equal">
              <Form.Field>
                <label>Max Concurrent Tunnels Per Peer</label>
                <Input
                  type="number"
                  min={1}
                  max={20}
                  value={vpnPolicy.maxConcurrentTunnelsPerPeer}
                  onChange={(e, { value }) => setVpnPolicy(prev => ({ ...prev, maxConcurrentTunnelsPerPeer: parseInt(value, 10) || 5 }))}
                  disabled={!vpnPolicy.enabled}
                />
              </Form.Field>
              <Form.Field>
                <label>Max Concurrent Tunnels (Pod Total)</label>
                <Input
                  type="number"
                  min={1}
                  max={100}
                  value={vpnPolicy.maxConcurrentTunnelsPod}
                  onChange={(e, { value }) => setVpnPolicy(prev => ({ ...prev, maxConcurrentTunnelsPod: parseInt(value, 10) || 15 }))}
                  disabled={!vpnPolicy.enabled}
                />
              </Form.Field>
            </Form.Group>

            <Form.Group widths="equal">
              <Form.Field>
                <label>Max New Tunnels Per Minute Per Peer</label>
                <Input
                  type="number"
                  min={1}
                  max={60}
                  value={vpnPolicy.maxNewTunnelsPerMinutePerPeer}
                  onChange={(e, { value }) => setVpnPolicy(prev => ({ ...prev, maxNewTunnelsPerMinutePerPeer: parseInt(value, 10) || 10 }))}
                  disabled={!vpnPolicy.enabled}
                />
              </Form.Field>
              <Form.Field>
                <label>Max Bandwidth Per Day Per Peer</label>
                <Input
                  type="number"
                  min={0}
                  value={Math.round(vpnPolicy.maxBytesPerDayPerPeer / (1024 * 1024))} // Convert to MB
                  onChange={(e, { value }) => setVpnPolicy(prev => ({ ...prev, maxBytesPerDayPerPeer: parseInt(value, 10) * 1024 * 1024 || 1073741824 }))}
                  disabled={!vpnPolicy.enabled}
                />
                <small>MB per day per peer ({formatBytes(vpnPolicy.maxBytesPerDayPerPeer)})</small>
              </Form.Field>
            </Form.Group>

            <Header as="h4">Timeouts</Header>
            <Form.Group widths="equal">
              <Form.Field>
                <label>Idle Timeout (HH:MM:SS)</label>
                <Input
                  placeholder="01:00:00"
                  value={vpnPolicy.idleTimeout}
                  onChange={(e, { value }) => setVpnPolicy(prev => ({ ...prev, idleTimeout: value }))}
                  disabled={!vpnPolicy.enabled}
                />
                <small>Close tunnels after this period of inactivity</small>
              </Form.Field>
              <Form.Field>
                <label>Max Lifetime (HH:MM:SS)</label>
                <Input
                  placeholder="24:00:00"
                  value={vpnPolicy.maxLifetime}
                  onChange={(e, { value }) => setVpnPolicy(prev => ({ ...prev, maxLifetime: value }))}
                  disabled={!vpnPolicy.enabled}
                />
                <small>Maximum duration a tunnel can remain open</small>
              </Form.Field>
              <Form.Field>
                <label>Dial Timeout (HH:MM:SS)</label>
                <Input
                  placeholder="00:00:30"
                  value={vpnPolicy.dialTimeout}
                  onChange={(e, { value }) => setVpnPolicy(prev => ({ ...prev, dialTimeout: value }))}
                  disabled={!vpnPolicy.enabled}
                />
                <small>Timeout for establishing outbound connections</small>
              </Form.Field>
            </Form.Group>
          </Form>
        </Tab.Pane>
      ),
    },
  ];

  return (
    <div>
      {error && (
        <Message error>
          <Message.Header>Configuration Error</Message.Header>
          <p>{error}</p>
        </Message>
      )}

      {success && (
        <Message success>
          <Message.Header>Configuration Updated</Message.Header>
          <p>{success}</p>
        </Message>
      )}

      <Tab menu={{ pointing: true }} panes={panes} />

      <div style={{ marginTop: '20px', textAlign: 'right' }}>
        <Button
          primary
          loading={saving}
          disabled={!vpnPolicy.enabled}
          onClick={handleSavePolicy}
        >
          Save VPN Configuration
        </Button>
      </div>

      {/* Add Destination Modal */}
      <Modal open={showAddDestination} onClose={() => setShowAddDestination(false)} size="small">
        <Modal.Header>Add Allowed Destination</Modal.Header>
        <Modal.Content>
          <Form>
            <Form.Field required>
              <label>Host Pattern</label>
              <Input
                placeholder="example.com or *.example.com"
                value={newDestination.hostPattern}
                onChange={(e, { value }) => setNewDestination(prev => ({ ...prev, hostPattern: value }))}
              />
              <small>Use exact hostname or wildcard patterns (e.g., *.domain.com)</small>
            </Form.Field>
            <Form.Field required>
              <label>Port</label>
              <Input
                type="number"
                min={1}
                max={65535}
                placeholder="80"
                value={newDestination.port}
                onChange={(e, { value }) => setNewDestination(prev => ({ ...prev, port: value }))}
              />
            </Form.Field>
            <Form.Field>
              <label>Protocol</label>
              <Dropdown
                selection
                options={protocolOptions}
                value={newDestination.protocol}
                onChange={(e, { value }) => setNewDestination(prev => ({ ...prev, protocol: value }))}
              />
            </Form.Field>
          </Form>
        </Modal.Content>
        <Modal.Actions>
          <Button onClick={() => setShowAddDestination(false)}>Cancel</Button>
          <Button
            primary
            onClick={handleAddDestination}
            disabled={!newDestination.hostPattern || !newDestination.port}
          >
            Add Destination
          </Button>
        </Modal.Actions>
      </Modal>

      {/* Add Service Modal */}
      <Modal open={showAddService} onClose={() => setShowAddService(false)} size="small">
        <Modal.Header>Add Registered Service</Modal.Header>
        <Modal.Content>
          <Form>
            <Form.Field required>
              <label>Service Name</label>
              <Input
                placeholder="My Web Service"
                value={newService.name}
                onChange={(e, { value }) => setNewService(prev => ({ ...prev, name: value }))}
              />
            </Form.Field>
            <Form.Field>
              <label>Description</label>
              <Input
                placeholder="Description of the service"
                value={newService.description}
                onChange={(e, { value }) => setNewService(prev => ({ ...prev, description: value }))}
              />
            </Form.Field>
            <Form.Field>
              <label>Service Type</label>
              <Dropdown
                selection
                options={serviceKindOptions}
                value={newService.kind}
                onChange={(e, { value }) => setNewService(prev => ({ ...prev, kind: value }))}
              />
            </Form.Field>
            <Form.Field required>
              <label>Destination Host</label>
              <Input
                placeholder="service.internal.company.com"
                value={newService.destinationHost}
                onChange={(e, { value }) => setNewService(prev => ({ ...prev, destinationHost: value }))}
              />
            </Form.Field>
            <Form.Field required>
              <label>Destination Port</label>
              <Input
                type="number"
                min={1}
                max={65535}
                placeholder="443"
                value={newService.destinationPort}
                onChange={(e, { value }) => setNewService(prev => ({ ...prev, destinationPort: value }))}
              />
            </Form.Field>
            <Form.Field>
              <label>Protocol</label>
              <Dropdown
                selection
                options={protocolOptions}
                value={newService.protocol}
                onChange={(e, { value }) => setNewService(prev => ({ ...prev, protocol: value }))}
              />
            </Form.Field>
          </Form>
        </Modal.Content>
        <Modal.Actions>
          <Button onClick={() => setShowAddService(false)}>Cancel</Button>
          <Button
            primary
            onClick={handleAddService}
            disabled={!newService.name || !newService.destinationHost || !newService.destinationPort}
          >
            Add Service
          </Button>
        </Modal.Actions>
      </Modal>
    </div>
  );
};

export default VpnGatewayConfig;


