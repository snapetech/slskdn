import * as pods from '../../lib/pods';
import React, { useEffect, useState } from 'react';
import {
  Button,
  Checkbox,
  Dropdown,
  Form,
  Header,
  Icon,
  Input,
  Label,
  Message,
  Modal,
  Segment,
  Tab,
  Table,
} from 'semantic-ui-react';

const VpnGatewayConfig = ({ podDetail, podId }) => {
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);

  // VPN Policy state
  const [vpnPolicy, setVpnPolicy] = useState({
    allowedDestinations: [],
    allowPrivateRanges: true,
    allowPublicDestinations: false,
    enabled: false,
    gatewayPeerId: '',
    // 1GB
    idleTimeout: '01:00:00',

    maxBytesPerDayPerPeer: 1_073_741_824,

    dialTimeout: '00:00:30',

    maxConcurrentTunnelsPerPeer: 5,

    maxConcurrentTunnelsPod: 15,

    maxLifetime: '24:00:00',
    maxMembers: 3,
    maxNewTunnelsPerMinutePerPeer: 10,
    registeredServices: [],
  });

  // Modal states for adding destinations and services
  const [showAddDestination, setShowAddDestination] = useState(false);
  const [showAddService, setShowAddService] = useState(false);
  const [newDestination, setNewDestination] = useState({
    hostPattern: '',
    port: '',
    protocol: 'tcp',
  });
  const [newService, setNewService] = useState({
    description: '',
    destinationHost: '',
    destinationPort: '',
    kind: 'WebInterface',
    name: '',
    protocol: 'tcp',
  });

  useEffect(() => {
    if (podDetail?.privateServicePolicy) {
      setVpnPolicy({
        ...podDetail.privateServicePolicy,

        allowedDestinations:
          podDetail.privateServicePolicy.allowedDestinations || [],

        allowPrivateRanges:
          podDetail.privateServicePolicy.allowPrivateRanges ?? true,

        allowPublicDestinations:
          podDetail.privateServicePolicy.allowPublicDestinations ?? false,

        gatewayPeerId: podDetail.privateServicePolicy.gatewayPeerId || '',

        idleTimeout: podDetail.privateServicePolicy.idleTimeout || '01:00:00',

        maxBytesPerDayPerPeer:
          podDetail.privateServicePolicy.maxBytesPerDayPerPeer || 1_073_741_824,

        dialTimeout: podDetail.privateServicePolicy.dialTimeout || '00:00:30',

        maxConcurrentTunnelsPerPeer:
          podDetail.privateServicePolicy.maxConcurrentTunnelsPerPeer || 5,

        maxConcurrentTunnelsPod:
          podDetail.privateServicePolicy.maxConcurrentTunnelsPod || 15,
        
maxLifetime: podDetail.privateServicePolicy.maxLifetime || '24:00:00',
        // Ensure defaults for missing fields
maxMembers: podDetail.privateServicePolicy.maxMembers || 3,
        maxNewTunnelsPerMinutePerPeer:
          podDetail.privateServicePolicy.maxNewTunnelsPerMinutePerPeer || 10,
        registeredServices:
          podDetail.privateServicePolicy.registeredServices || [],
      });
    }
  }, [podDetail]);

  const hasVpnCapability = podDetail?.capabilities?.includes(
    'PrivateServiceGateway',
  );

  const handleSavePolicy = async () => {
    if (!podId) return;

    setSaving(true);
    setError(null);
    setSuccess(null);

    try {
      // Create updated pod with VPN policy
      const updatedPod = {
        ...podDetail,
        privateServicePolicy: vpnPolicy.enabled ? vpnPolicy : null,
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
        port: Number.parseInt(newDestination.port, 10),
        protocol: newDestination.protocol,
      },
    ];

    setVpnPolicy((previous) => ({
      ...previous,
      allowedDestinations: updatedDestinations,
    }));
    setNewDestination({ hostPattern: '', port: '', protocol: 'tcp' });
    setShowAddDestination(false);
  };

  const handleRemoveDestination = (index) => {
    const updatedDestinations = vpnPolicy.allowedDestinations.filter(
      (_, index_) => index_ !== index,
    );
    setVpnPolicy((previous) => ({
      ...previous,
      allowedDestinations: updatedDestinations,
    }));
  };

  const handleAddService = () => {
    if (
      !newService.name ||
      !newService.destinationHost ||
      !newService.destinationPort
    )
      return;

    const updatedServices = [
      ...vpnPolicy.registeredServices,
      {
        description: newService.description,
        destinationHost: newService.destinationHost,
        destinationPort: Number.parseInt(newService.destinationPort, 10),
        kind: newService.kind,
        name: newService.name,
        protocol: newService.protocol,
      },
    ];

    setVpnPolicy((previous) => ({
      ...previous,
      registeredServices: updatedServices,
    }));
    setNewService({
      description: '',
      destinationHost: '',
      destinationPort: '',
      kind: 'WebInterface',
      name: '',
      protocol: 'tcp',
    });
    setShowAddService(false);
  };

  const handleRemoveService = (index) => {
    const updatedServices = vpnPolicy.registeredServices.filter(
      (_, index_) => index_ !== index,
    );
    setVpnPolicy((previous) => ({
      ...previous,
      registeredServices: updatedServices,
    }));
  };

  const formatBytes = (bytes) => {
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let value = bytes;
    let unitIndex = 0;

    while (value >= 1_024 && unitIndex < units.length - 1) {
      value /= 1_024;
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
          <p>
            To enable VPN functionality, add the "PrivateServiceGateway"
            capability to the pod.
          </p>
        </Segment.Inline>
      </Segment>
    );
  }

  const serviceKindOptions = [
    { key: 'WebInterface', text: 'Web Interface', value: 'WebInterface' },
    { key: 'Database', text: 'Database', value: 'Database' },
    { key: 'SSH', text: 'SSH', value: 'SSH' },
    { key: 'Custom', text: 'Custom', value: 'Custom' },
  ];

  const protocolOptions = [
    { key: 'tcp', text: 'TCP', value: 'tcp' },
    { key: 'udp', text: 'UDP', value: 'udp' },
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
                  checked={vpnPolicy.enabled}
                  onChange={(e, { checked }) =>
                    setVpnPolicy((previous) => ({
                      ...previous,
                      enabled: checked,
                    }))
                  }
                  toggle
                />
              </Form.Field>
              <Form.Field width={4}>
                <label>Max Pod Members</label>
                <Input
                  disabled={!vpnPolicy.enabled}
                  max={3}
                  min={1}
                  onChange={(e, { value }) =>
                    setVpnPolicy((previous) => ({
                      ...previous,
                      maxMembers: Number.parseInt(value, 10) || 3,
                    }))
                  }
                  type="number"
                  value={vpnPolicy.maxMembers}
                />
                <small>Hard limit of 3 for VPN-enabled pods</small>
              </Form.Field>
              <Form.Field width={8}>
                <label>Gateway Peer ID</label>
                <Input
                  disabled={!vpnPolicy.enabled}
                  onChange={(e, { value }) =>
                    setVpnPolicy((previous) => ({
                      ...previous,
                      gatewayPeerId: value,
                    }))
                  }
                  placeholder="peer-id-of-gateway-node"
                  value={vpnPolicy.gatewayPeerId}
                />
              </Form.Field>
            </Form.Group>

            <Header as="h4">Network Access Control</Header>
            <Form.Group>
              <Form.Field>
                <Checkbox
                  checked={vpnPolicy.allowPrivateRanges}
                  disabled={!vpnPolicy.enabled}
                  label="Allow private IP ranges (RFC1918)"
                  onChange={(e, { checked }) =>
                    setVpnPolicy((previous) => ({
                      ...previous,
                      allowPrivateRanges: checked,
                    }))
                  }
                />
              </Form.Field>
              <Form.Field>
                <Checkbox
                  checked={vpnPolicy.allowPublicDestinations}
                  disabled={!vpnPolicy.enabled}
                  label="Allow public internet destinations"
                  onChange={(e, { checked }) =>
                    setVpnPolicy((previous) => ({
                      ...previous,
                      allowPublicDestinations: checked,
                    }))
                  }
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
              content="Add Destination"
              disabled={!vpnPolicy.enabled}
              icon="plus"
              onClick={() => setShowAddDestination(true)}
              primary
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
              {vpnPolicy.allowedDestinations.map((destination, index) => (
                <Table.Row key={index}>
                  <Table.Cell>{destination.hostPattern}</Table.Cell>
                  <Table.Cell>{destination.port}</Table.Cell>
                  <Table.Cell>{destination.protocol?.toUpperCase()}</Table.Cell>
                  <Table.Cell>
                    <Button
                      color="red"
                      disabled={!vpnPolicy.enabled}
                      icon="trash"
                      onClick={() => handleRemoveDestination(index)}
                      size="small"
                    />
                  </Table.Cell>
                </Table.Row>
              ))}
              {vpnPolicy.allowedDestinations.length === 0 && (
                <Table.Row>
                  <Table.Cell
                    colSpan={4}
                    textAlign="center"
                  >
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
              content="Add Service"
              disabled={!vpnPolicy.enabled}
              icon="plus"
              onClick={() => setShowAddService(true)}
              primary
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
                    {service.destinationHost}:{service.destinationPort} (
                    {service.protocol})
                  </Table.Cell>
                  <Table.Cell>
                    <Button
                      color="red"
                      disabled={!vpnPolicy.enabled}
                      icon="trash"
                      onClick={() => handleRemoveService(index)}
                      size="small"
                    />
                  </Table.Cell>
                </Table.Row>
              ))}
              {vpnPolicy.registeredServices.length === 0 && (
                <Table.Row>
                  <Table.Cell
                    colSpan={5}
                    textAlign="center"
                  >
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
                  disabled={!vpnPolicy.enabled}
                  max={20}
                  min={1}
                  onChange={(e, { value }) =>
                    setVpnPolicy((previous) => ({
                      ...previous,
                      maxConcurrentTunnelsPerPeer:
                        Number.parseInt(value, 10) || 5,
                    }))
                  }
                  type="number"
                  value={vpnPolicy.maxConcurrentTunnelsPerPeer}
                />
              </Form.Field>
              <Form.Field>
                <label>Max Concurrent Tunnels (Pod Total)</label>
                <Input
                  disabled={!vpnPolicy.enabled}
                  max={100}
                  min={1}
                  onChange={(e, { value }) =>
                    setVpnPolicy((previous) => ({
                      ...previous,
                      maxConcurrentTunnelsPod: Number.parseInt(value, 10) || 15,
                    }))
                  }
                  type="number"
                  value={vpnPolicy.maxConcurrentTunnelsPod}
                />
              </Form.Field>
            </Form.Group>

            <Form.Group widths="equal">
              <Form.Field>
                <label>Max New Tunnels Per Minute Per Peer</label>
                <Input
                  disabled={!vpnPolicy.enabled}
                  max={60}
                  min={1}
                  onChange={(e, { value }) =>
                    setVpnPolicy((previous) => ({
                      ...previous,
                      maxNewTunnelsPerMinutePerPeer:
                        Number.parseInt(value, 10) || 10,
                    }))
                  }
                  type="number"
                  value={vpnPolicy.maxNewTunnelsPerMinutePerPeer}
                />
              </Form.Field>
              <Form.Field>
                <label>Max Bandwidth Per Day Per Peer</label>
                <Input
                  disabled={!vpnPolicy.enabled}
                  min={0}
                  onChange={(e, { value }) =>
                    setVpnPolicy((previous) => ({
                      ...previous,
                      maxBytesPerDayPerPeer:
                        Number.parseInt(value, 10) * 1_024 * 1_024 ||
                        1_073_741_824,
                    }))
                  }
                  type="number"
                  value={Math.round(
                    vpnPolicy.maxBytesPerDayPerPeer / (1_024 * 1_024),
                  )} // Convert to MB
                />
                <small>
                  MB per day per peer (
                  {formatBytes(vpnPolicy.maxBytesPerDayPerPeer)})
                </small>
              </Form.Field>
            </Form.Group>

            <Header as="h4">Timeouts</Header>
            <Form.Group widths="equal">
              <Form.Field>
                <label>Idle Timeout (HH:MM:SS)</label>
                <Input
                  disabled={!vpnPolicy.enabled}
                  onChange={(e, { value }) =>
                    setVpnPolicy((previous) => ({
                      ...previous,
                      idleTimeout: value,
                    }))
                  }
                  placeholder="01:00:00"
                  value={vpnPolicy.idleTimeout}
                />
                <small>Close tunnels after this period of inactivity</small>
              </Form.Field>
              <Form.Field>
                <label>Max Lifetime (HH:MM:SS)</label>
                <Input
                  disabled={!vpnPolicy.enabled}
                  onChange={(e, { value }) =>
                    setVpnPolicy((previous) => ({
                      ...previous,
                      maxLifetime: value,
                    }))
                  }
                  placeholder="24:00:00"
                  value={vpnPolicy.maxLifetime}
                />
                <small>Maximum duration a tunnel can remain open</small>
              </Form.Field>
              <Form.Field>
                <label>Dial Timeout (HH:MM:SS)</label>
                <Input
                  disabled={!vpnPolicy.enabled}
                  onChange={(e, { value }) =>
                    setVpnPolicy((previous) => ({
                      ...previous,
                      dialTimeout: value,
                    }))
                  }
                  placeholder="00:00:30"
                  value={vpnPolicy.dialTimeout}
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

      <Tab
        menu={{ pointing: true }}
        panes={panes}
      />

      <div style={{ marginTop: '20px', textAlign: 'right' }}>
        <Button
          disabled={!vpnPolicy.enabled}
          loading={saving}
          onClick={handleSavePolicy}
          primary
        >
          Save VPN Configuration
        </Button>
      </div>

      {/* Add Destination Modal */}
      <Modal
        onClose={() => setShowAddDestination(false)}
        open={showAddDestination}
        size="small"
      >
        <Modal.Header>Add Allowed Destination</Modal.Header>
        <Modal.Content>
          <Form>
            <Form.Field required>
              <label>Host Pattern</label>
              <Input
                onChange={(e, { value }) =>
                  setNewDestination((previous) => ({
                    ...previous,
                    hostPattern: value,
                  }))
                }
                placeholder="example.com or *.example.com"
                value={newDestination.hostPattern}
              />
              <small>
                Use exact hostname or wildcard patterns (e.g., *.domain.com)
              </small>
            </Form.Field>
            <Form.Field required>
              <label>Port</label>
              <Input
                max={65_535}
                min={1}
                onChange={(e, { value }) =>
                  setNewDestination((previous) => ({
                    ...previous,
                    port: value,
                  }))
                }
                placeholder="80"
                type="number"
                value={newDestination.port}
              />
            </Form.Field>
            <Form.Field>
              <label>Protocol</label>
              <Dropdown
                onChange={(e, { value }) =>
                  setNewDestination((previous) => ({
                    ...previous,
                    protocol: value,
                  }))
                }
                options={protocolOptions}
                selection
                value={newDestination.protocol}
              />
            </Form.Field>
          </Form>
        </Modal.Content>
        <Modal.Actions>
          <Button onClick={() => setShowAddDestination(false)}>Cancel</Button>
          <Button
            disabled={!newDestination.hostPattern || !newDestination.port}
            onClick={handleAddDestination}
            primary
          >
            Add Destination
          </Button>
        </Modal.Actions>
      </Modal>

      {/* Add Service Modal */}
      <Modal
        onClose={() => setShowAddService(false)}
        open={showAddService}
        size="small"
      >
        <Modal.Header>Add Registered Service</Modal.Header>
        <Modal.Content>
          <Form>
            <Form.Field required>
              <label>Service Name</label>
              <Input
                onChange={(e, { value }) =>
                  setNewService((previous) => ({ ...previous, name: value }))
                }
                placeholder="My Web Service"
                value={newService.name}
              />
            </Form.Field>
            <Form.Field>
              <label>Description</label>
              <Input
                onChange={(e, { value }) =>
                  setNewService((previous) => ({
                    ...previous,
                    description: value,
                  }))
                }
                placeholder="Description of the service"
                value={newService.description}
              />
            </Form.Field>
            <Form.Field>
              <label>Service Type</label>
              <Dropdown
                onChange={(e, { value }) =>
                  setNewService((previous) => ({ ...previous, kind: value }))
                }
                options={serviceKindOptions}
                selection
                value={newService.kind}
              />
            </Form.Field>
            <Form.Field required>
              <label>Destination Host</label>
              <Input
                onChange={(e, { value }) =>
                  setNewService((previous) => ({
                    ...previous,
                    destinationHost: value,
                  }))
                }
                placeholder="service.internal.company.com"
                value={newService.destinationHost}
              />
            </Form.Field>
            <Form.Field required>
              <label>Destination Port</label>
              <Input
                max={65_535}
                min={1}
                onChange={(e, { value }) =>
                  setNewService((previous) => ({
                    ...previous,
                    destinationPort: value,
                  }))
                }
                placeholder="443"
                type="number"
                value={newService.destinationPort}
              />
            </Form.Field>
            <Form.Field>
              <label>Protocol</label>
              <Dropdown
                onChange={(e, { value }) =>
                  setNewService((previous) => ({
                    ...previous,
                    protocol: value,
                  }))
                }
                options={protocolOptions}
                selection
                value={newService.protocol}
              />
            </Form.Field>
          </Form>
        </Modal.Content>
        <Modal.Actions>
          <Button onClick={() => setShowAddService(false)}>Cancel</Button>
          <Button
            disabled={
              !newService.name ||
              !newService.destinationHost ||
              !newService.destinationPort
            }
            onClick={handleAddService}
            primary
          >
            Add Service
          </Button>
        </Modal.Actions>
      </Modal>
    </div>
  );
};

export default VpnGatewayConfig;
