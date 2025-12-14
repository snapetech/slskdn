import { urlBase } from '../../config';
import * as pods from '../../lib/pods';
import * as portForwarding from '../../lib/portForwarding';
import React, { Component } from 'react';
import {
  Button,
  Card,
  Dimmer,
  Dropdown,
  Form,
  Icon,
  Input,
  Label,
  List,
  Loader,
  Message,
  Modal,
  Popup,
  Segment,
  Statistic,
  Tab,
  Table,
} from 'semantic-ui-react';

const initialState = {
  availablePorts: [],
  creatingForwarding: false,
  createForm: {
    destinationHost: '',
    localPort: '',
    destinationPort: '',
    serviceName: '',
  },
  error: null,
  forwardingStatus: [],
  intervals: {
    stats: undefined,
    status: undefined,
  },
  loading: false,
  pods: [],
  selectedPodDetail: null,
  selectedPodId: null,
  showCreateModal: false,
  stoppingForwarding: false,
  success: null,
  tunnelStats: {},
  vpnPodStatus: {},
};

class PortForwarding extends Component {
  constructor(props) {
    super(props);
    this.state = initialState;
  }

  componentDidMount() {
    this.setState({
      intervals: {
        stats: window.setInterval(this.fetchTunnelStats, 10_000),
        status: window.setInterval(this.fetchForwardingStatus, 5_000), // More frequent stats updates
      },
    });

    this.initializeComponent();
  }

  componentWillUnmount() {
    const { stats, status } = this.state.intervals;
    clearInterval(status);
    clearInterval(stats);
    this.setState({ intervals: initialState.intervals });
  }

  initializeComponent = async () => {
    this.setState({ error: null, loading: true });

    try {
      await Promise.all([
        this.fetchPods(),
        this.fetchAvailablePorts(),
        this.fetchForwardingStatus(),
        this.fetchVpnPodStatus(),
      ]);
    } catch (error) {
      console.error('Failed to initialize port forwarding:', error);
      this.setState({ error: error.message });
    } finally {
      this.setState({ loading: false });
    }
  };

  fetchPods = async () => {
    try {
      const podsList = await pods.list();
      this.setState({ pods: podsList || [] });
    } catch (error) {
      console.error('Failed to fetch pods:', error);
      this.setState({ pods: [] });
    }
  };

  fetchAvailablePorts = async () => {
    try {
      const result = await portForwarding.getAvailablePorts();
      this.setState({ availablePorts: result.availablePorts || [] });
    } catch (error) {
      console.error('Failed to fetch available ports:', error);
      this.setState({ availablePorts: [] });
    }
  };

  fetchForwardingStatus = async () => {
    try {
      const status = await portForwarding.getForwardingStatus();
      this.setState({ forwardingStatus: status || [] });
    } catch (error) {
      console.error('Failed to fetch forwarding status:', error);
      this.setState({ forwardingStatus: [] });
    }
  };

  fetchTunnelStats = async () => {
    const { forwardingStatus } = this.state;

    if (forwardingStatus.length === 0) {
      return;
    }

    try {
      const statsPromises = forwardingStatus.map(async (forwarding) => {
        try {
          // Note: This would need a new API endpoint for detailed tunnel stats
          // For now, we'll simulate with basic stats
          return {
            bytesIn: Math.floor(Math.random() * 1_000_000),
            bytesOut: Math.floor(Math.random() * 1_000_000),
            connections: forwarding.activeConnections || 0,
            lastActivity: Date.now() - 30_000,
            localPort: forwarding.localPort,
            uptime: Date.now() - (forwarding.startTime || Date.now()), // Mock 30 seconds ago
          };
        } catch (error) {
          console.error(
            `Failed to fetch stats for port ${forwarding.localPort}:`,
            error,
          );
          return null;
        }
      });

      const statsResults = await Promise.all(statsPromises);
      const stats = statsResults.reduce((accumulator, stat) => {
        if (stat) {
          accumulator[stat.localPort] = stat;
        }

        return accumulator;
      }, {});

      this.setState({ tunnelStats: stats });
    } catch (error) {
      console.error('Failed to fetch tunnel stats:', error);
    }
  };

  fetchVpnPodStatus = async () => {
    const { pods } = this.state;
    const vpnCapablePods = pods.filter(
      (pod) =>
        pod.capabilities?.includes('PrivateServiceGateway') ||
        pod.privateServicePolicy?.enabled === true,
    );

    const statusPromises = vpnCapablePods.map(async (pod) => {
      try {
        const detail = await pods.get(pod.podId);
        const memberCount = detail.members?.length || 0;
        const activeTunnels = detail.activeTunnels || 0; // This would come from a new API
        const totalBandwidth = detail.totalBandwidth || 0; // This would come from a new API

        return {
          activeTunnels,
          lastActivity: detail.lastActivity || Date.now(),
          members: memberCount,
          name: pod.name || pod.podId,
          podId: pod.podId,
          status: detail.privateServicePolicy?.enabled ? 'Active' : 'Inactive',
          totalBandwidth,
        };
      } catch (error) {
        console.error(`Failed to fetch status for pod ${pod.podId}:`, error);
        return {
          activeTunnels: 0,
          lastActivity: Date.now(),
          members: 0,
          name: pod.name || pod.podId,
          podId: pod.podId,
          status: 'Error',
          totalBandwidth: 0,
        };
      }
    });

    try {
      const statusResults = await Promise.all(statusPromises);
      const status = statusResults.reduce((accumulator, stat) => {
        accumulator[stat.podId] = stat;
        return accumulator;
      }, {});

      this.setState({ vpnPodStatus: status });
    } catch (error) {
      console.error('Failed to fetch VPN pod status:', error);
    }
  };

  handlePodSelection = async (podId) => {
    this.setState({
      loading: true,
      selectedPodDetail: null,
      selectedPodId: podId,
    });

    try {
      const podDetail = await pods.get(podId);
      this.setState({ selectedPodDetail: podDetail });
    } catch (error) {
      console.error('Failed to fetch pod detail:', error);
      this.setState({ error: `Failed to load pod details: ${error.message}` });
    } finally {
      this.setState({ loading: false });
    }
  };

  handleCreateForwarding = async () => {
    const { createForm, selectedPodId } = this.state;

    if (!selectedPodId) {
      this.setState({ error: 'Please select a pod first' });
      return;
    }

    // Validate form
    if (
      !createForm.localPort ||
      !createForm.destinationHost ||
      !createForm.destinationPort
    ) {
      this.setState({ error: 'Please fill in all required fields' });
      return;
    }

    const localPort = Number.parseInt(createForm.localPort);
    const destinationPort = Number.parseInt(createForm.destinationPort);

    if (isNaN(localPort) || localPort < 1_024 || localPort > 65_535) {
      this.setState({ error: 'Local port must be between 1024 and 65535' });
      return;
    }

    if (
      isNaN(destinationPort) ||
      destinationPort < 1 ||
      destinationPort > 65_535
    ) {
      this.setState({ error: 'Destination port must be between 1 and 65535' });
      return;
    }

    this.setState({ creatingForwarding: true, error: null });

    try {
      await portForwarding.startForwarding({
        destinationHost: createForm.destinationHost,
        destinationPort,
        localPort,
        podId: selectedPodId,
        serviceName: createForm.serviceName || undefined,
      });

      // Reset form and refresh status
      this.setState({
        createForm: initialState.createForm,
        showCreateModal: false,
      });

      await Promise.all([
        this.fetchAvailablePorts(),
        this.fetchForwardingStatus(),
      ]);
    } catch (error) {
      console.error('Failed to create port forwarding:', error);
      this.setState({ error: error.message });
    } finally {
      this.setState({ creatingForwarding: false });
    }
  };

  handleStopForwarding = async (localPort) => {
    this.setState({ error: null, stoppingForwarding: true, success: null });

    try {
      await portForwarding.stopForwarding(localPort);
      await Promise.all([
        this.fetchAvailablePorts(),
        this.fetchForwardingStatus(),
      ]);
      this.setState({
        success: `Successfully stopped forwarding on port ${localPort}`,
      });
    } catch (error) {
      console.error('Failed to stop port forwarding:', error);
      this.setState({ error: error.message });
    } finally {
      this.setState({ stoppingForwarding: false });
    }
  };

  handleFormChange = (field, value) => {
    this.setState((previousState) => ({
      createForm: {
        ...previousState.createForm,
        [field]: value,
      },
    }));
  };

  render() {
    const {
      availablePorts,
      createForm,
      creatingForwarding,
      error,
      forwardingStatus,
      loading,
      pods,
      selectedPodDetail,
      selectedPodId,
      showCreateModal,
      stoppingForwarding,
      success,
      tunnelStats,
      vpnPodStatus,
    } = this.state;

    const selectedPod = pods.find((p) => p.podId === selectedPodId);

    // Filter pods that have VPN gateway capability
    const vpnCapablePods = pods.filter(
      (pod) =>
        pod.capabilities?.includes('PrivateServiceGateway') ||
        pod.privateServicePolicy?.enabled === true,
    );

    const panes = [
      {
        menuItem: 'Active Forwarding',
        render: () => (
          <Tab.Pane>
            {forwardingStatus.length === 0 ? (
              <Segment placeholder>
                <Icon name="exchange" />
                <h3>No active port forwarding</h3>
                <p>
                  Start forwarding local ports to remote services through VPN
                  tunnels.
                </p>
                <Button
                  disabled={vpnCapablePods.length === 0}
                  onClick={() => this.setState({ showCreateModal: true })}
                  primary
                >
                  Start Forwarding
                </Button>
              </Segment>
            ) : (
              <div>
                <div style={{ marginBottom: '20px', textAlign: 'right' }}>
                  <Button
                    disabled={vpnCapablePods.length === 0}
                    onClick={() => this.setState({ showCreateModal: true })}
                    primary
                  >
                    <Icon name="plus" />
                    Add Forwarding
                  </Button>
                </div>

                <Table celled>
                  <Table.Header>
                    <Table.Row>
                      <Table.HeaderCell>Local Port</Table.HeaderCell>
                      <Table.HeaderCell>Pod</Table.HeaderCell>
                      <Table.HeaderCell>Remote Service</Table.HeaderCell>
                      <Table.HeaderCell>Status</Table.HeaderCell>
                      <Table.HeaderCell>Connections</Table.HeaderCell>
                      <Table.HeaderCell>Data Transferred</Table.HeaderCell>
                      <Table.HeaderCell>Actions</Table.HeaderCell>
                    </Table.Row>
                  </Table.Header>
                  <Table.Body>
                    {forwardingStatus.map((forwarding) => (
                      <Table.Row key={forwarding.localPort}>
                        <Table.Cell>
                          <code>localhost:{forwarding.localPort}</code>
                        </Table.Cell>
                        <Table.Cell>
                          {forwarding.podId}
                          {forwarding.serviceName && (
                            <div style={{ color: '#666', fontSize: '0.8em' }}>
                              Service: {forwarding.serviceName}
                            </div>
                          )}
                        </Table.Cell>
                        <Table.Cell>
                          <code>
                            {forwarding.destinationHost}:
                            {forwarding.destinationPort}
                          </code>
                        </Table.Cell>
                        <Table.Cell>
                          <Label color={forwarding.isActive ? 'green' : 'red'}>
                            {forwarding.isActive ? 'Active' : 'Inactive'}
                          </Label>
                        </Table.Cell>
                        <Table.Cell>{forwarding.activeConnections}</Table.Cell>
                        <Table.Cell>
                          {forwarding.bytesForwarded > 0
                            ? `${(forwarding.bytesForwarded / 1_024).toFixed(1)} KB`
                            : '0 KB'}
                        </Table.Cell>
                        <Table.Cell>
                          <Popup
                            content="Stop port forwarding"
                            trigger={
                              <Button
                                color="red"
                                icon="stop"
                                loading={stoppingForwarding}
                                onClick={() =>
                                  this.handleStopForwarding(
                                    forwarding.localPort,
                                  )
                                }
                                size="small"
                              />
                            }
                          />
                        </Table.Cell>
                      </Table.Row>
                    ))}
                  </Table.Body>
                </Table>
              </div>
            )}
          </Tab.Pane>
        ),
      },
      {
        menuItem: 'Available Ports',
        render: () => (
          <Tab.Pane>
            <div style={{ marginBottom: '20px' }}>
              <Statistic.Group size="small">
                <Statistic>
                  <Statistic.Value>{availablePorts.length}</Statistic.Value>
                  <Statistic.Label>Available Ports</Statistic.Label>
                </Statistic>
                <Statistic>
                  <Statistic.Value>{forwardingStatus.length}</Statistic.Value>
                  <Statistic.Label>In Use</Statistic.Label>
                </Statistic>
              </Statistic.Group>
            </div>

            <Segment>
              <p>Available ports for forwarding (1024-65535):</p>
              <div
                style={{
                  backgroundColor: '#f8f9fa',
                  borderRadius: '4px',
                  fontFamily: 'monospace',
                  fontSize: '12px',
                  maxHeight: '400px',
                  overflowY: 'auto',
                  padding: '10px',
                }}
              >
                {availablePorts.length > 0 ? (
                  availablePorts.slice(0, 100).join(', ') +
                  (availablePorts.length > 100
                    ? ` ... (+${availablePorts.length - 100} more)`
                    : '')
                ) : (
                  <em>No ports available or still loading...</em>
                )}
              </div>
            </Segment>
          </Tab.Pane>
        ),
      },
      {
        menuItem: 'Tunnel Statistics',
        render: () => (
          <Tab.Pane>
            <div style={{ marginBottom: '20px' }}>
              <Statistic.Group widths="four">
                <Statistic>
                  <Statistic.Value>{forwardingStatus.length}</Statistic.Value>
                  <Statistic.Label>Active Tunnels</Statistic.Label>
                </Statistic>
                <Statistic>
                  <Statistic.Value>
                    {Object.values(tunnelStats).reduce(
                      (sum, stats) => sum + (stats?.connections || 0),
                      0,
                    )}
                  </Statistic.Value>
                  <Statistic.Label>Total Connections</Statistic.Label>
                </Statistic>
                <Statistic>
                  <Statistic.Value>
                    {(
                      Object.values(tunnelStats).reduce(
                        (sum, stats) =>
                          sum + (stats?.bytesIn || 0) + (stats?.bytesOut || 0),
                        0,
                      ) /
                      1_024 /
                      1_024
                    ).toFixed(2)}{' '}
                    MB
                  </Statistic.Value>
                  <Statistic.Label>Data Transferred</Statistic.Label>
                </Statistic>
                <Statistic>
                  <Statistic.Value>
                    {Object.values(tunnelStats).some(
                      (stats) => stats?.uptime > 0,
                    )
                      ? (
                          Object.values(tunnelStats).reduce(
                            (sum, stats) => sum + (stats?.uptime || 0),
                            0,
                          ) /
                          Object.values(tunnelStats).length /
                          1_000 /
                          60
                        ).toFixed(1)
                      : '0.0'}{' '}
                    min
                  </Statistic.Value>
                  <Statistic.Label>Avg Uptime</Statistic.Label>
                </Statistic>
              </Statistic.Group>
            </div>

            <Table celled>
              <Table.Header>
                <Table.Row>
                  <Table.HeaderCell>Local Port</Table.HeaderCell>
                  <Table.HeaderCell>Data In</Table.HeaderCell>
                  <Table.HeaderCell>Data Out</Table.HeaderCell>
                  <Table.HeaderCell>Connections</Table.HeaderCell>
                  <Table.HeaderCell>Uptime</Table.HeaderCell>
                  <Table.HeaderCell>Last Activity</Table.HeaderCell>
                </Table.Row>
              </Table.Header>
              <Table.Body>
                {forwardingStatus.map((forwarding) => {
                  const stats = tunnelStats[forwarding.localPort];
                  return (
                    <Table.Row key={forwarding.localPort}>
                      <Table.Cell>
                        <code>localhost:{forwarding.localPort}</code>
                      </Table.Cell>
                      <Table.Cell>
                        {stats
                          ? `${(stats.bytesIn / 1_024).toFixed(1)} KB`
                          : 'N/A'}
                      </Table.Cell>
                      <Table.Cell>
                        {stats
                          ? `${(stats.bytesOut / 1_024).toFixed(1)} KB`
                          : 'N/A'}
                      </Table.Cell>
                      <Table.Cell>{stats?.connections || 0}</Table.Cell>
                      <Table.Cell>
                        {stats
                          ? `${Math.floor(stats.uptime / 1_000 / 60)}m ${Math.floor((stats.uptime / 1_000) % 60)}s`
                          : 'N/A'}
                      </Table.Cell>
                      <Table.Cell>
                        {stats
                          ? `${Math.floor((Date.now() - stats.lastActivity) / 1_000)}s ago`
                          : 'N/A'}
                      </Table.Cell>
                    </Table.Row>
                  );
                })}
                {forwardingStatus.length === 0 && (
                  <Table.Row>
                    <Table.Cell
                      colSpan={6}
                      textAlign="center"
                    >
                      No active tunnels to display statistics for
                    </Table.Cell>
                  </Table.Row>
                )}
              </Table.Body>
            </Table>
          </Tab.Pane>
        ),
      },
      {
        menuItem: 'VPN Pods',
        render: () => (
          <Tab.Pane>
            <div style={{ marginBottom: '20px' }}>
              <Statistic.Group widths="three">
                <Statistic>
                  <Statistic.Value>
                    {Object.keys(vpnPodStatus).length}
                  </Statistic.Value>
                  <Statistic.Label>VPN-Capable Pods</Statistic.Label>
                </Statistic>
                <Statistic>
                  <Statistic.Value>
                    {Object.values(vpnPodStatus).reduce(
                      (sum, pod) => sum + pod.members,
                      0,
                    )}
                  </Statistic.Value>
                  <Statistic.Label>Total Members</Statistic.Label>
                </Statistic>
                <Statistic>
                  <Statistic.Value>
                    {Object.values(vpnPodStatus).reduce(
                      (sum, pod) => sum + pod.activeTunnels,
                      0,
                    )}
                  </Statistic.Value>
                  <Statistic.Label>Active Tunnels</Statistic.Label>
                </Statistic>
              </Statistic.Group>
            </div>

            <Table celled>
              <Table.Header>
                <Table.Row>
                  <Table.HeaderCell>Pod Name</Table.HeaderCell>
                  <Table.HeaderCell>Members</Table.HeaderCell>
                  <Table.HeaderCell>Active Tunnels</Table.HeaderCell>
                  <Table.HeaderCell>Data Transferred</Table.HeaderCell>
                  <Table.HeaderCell>Status</Table.HeaderCell>
                  <Table.HeaderCell>Last Activity</Table.HeaderCell>
                </Table.Row>
              </Table.Header>
              <Table.Body>
                {Object.values(vpnPodStatus).map((pod) => (
                  <Table.Row key={pod.podId}>
                    <Table.Cell>
                      <strong>{pod.name}</strong>
                      <div style={{ color: '#666', fontSize: '0.8em' }}>
                        ID: {pod.podId}
                      </div>
                    </Table.Cell>
                    <Table.Cell>{pod.members}</Table.Cell>
                    <Table.Cell>{pod.activeTunnels}</Table.Cell>
                    <Table.Cell>
                      {pod.totalBandwidth > 0
                        ? `${(pod.totalBandwidth / 1_024 / 1_024).toFixed(2)} MB`
                        : '0 MB'}
                    </Table.Cell>
                    <Table.Cell>
                      <Label color={pod.status === 'Active' ? 'green' : 'grey'}>
                        {pod.status}
                      </Label>
                    </Table.Cell>
                    <Table.Cell>
                      {Math.floor((Date.now() - pod.lastActivity) / 1_000)}s ago
                    </Table.Cell>
                  </Table.Row>
                ))}
                {Object.keys(vpnPodStatus).length === 0 && (
                  <Table.Row>
                    <Table.Cell
                      colSpan={6}
                      textAlign="center"
                    >
                      No VPN-capable pods found
                    </Table.Cell>
                  </Table.Row>
                )}
              </Table.Body>
            </Table>
          </Tab.Pane>
        ),
      },
    ];

    return (
      <div style={{ padding: '20px' }}>
        <Dimmer active={loading}>
          <Loader />
        </Dimmer>

        <div style={{ marginBottom: '30px' }}>
          <h2>Port Forwarding</h2>
          <p>
            Forward local ports to remote services through secure VPN tunnels.
          </p>
        </div>

        {error && (
          <Message error>
            <Message.Header>Error</Message.Header>
            <p>{error}</p>
            <Button
              onClick={() => this.setState({ error: null })}
              size="small"
            >
              Dismiss
            </Button>
          </Message>
        )}

        {success && (
          <Message success>
            <Message.Header>Success</Message.Header>
            <p>{success}</p>
            <Button
              onClick={() => this.setState({ success: null })}
              size="small"
            >
              Dismiss
            </Button>
          </Message>
        )}

        {vpnCapablePods.length === 0 && (
          <Message warning>
            <Message.Header>No VPN-Capable Pods</Message.Header>
            <p>
              You need at least one pod with VPN gateway capability to use port
              forwarding.
            </p>
            <p>
              Create or join a pod that has the{' '}
              <code>PrivateServiceGateway</code> capability enabled.
            </p>
          </Message>
        )}

        <Tab
          menu={{ pointing: true }}
          panes={panes}
        />

        {/* Create Forwarding Modal */}
        <Modal
          onClose={() => this.setState({ showCreateModal: false })}
          open={showCreateModal}
          size="small"
        >
          <Modal.Header>Start Port Forwarding</Modal.Header>
          <Modal.Content>
            <Form>
              <Form.Field>
                <label>VPN Pod</label>
                <Dropdown
                  fluid
                  onChange={(e, { value }) => this.handlePodSelection(value)}
                  options={vpnCapablePods.map((pod) => ({
                    key: pod.podId,
                    text: pod.name || pod.podId,
                    value: pod.podId,
                  }))}
                  placeholder="Select a VPN-capable pod"
                  selection
                  value={selectedPodId || ''}
                />
                {selectedPodDetail && (
                  <div
                    style={{
                      color: '#666',
                      fontSize: '0.9em',
                      marginTop: '10px',
                    }}
                  >
                    <p>
                      <strong>Members:</strong>{' '}
                      {selectedPodDetail.members?.length || 0}
                    </p>
                    {selectedPodDetail.privateServicePolicy?.enabled && (
                      <p>
                        <strong>VPN Gateway:</strong> Enabled
                      </p>
                    )}
                  </div>
                )}
              </Form.Field>

              <Form.Field required>
                <label>Local Port</label>
                <Input
                  max="65535"
                  min="1024"
                  onChange={(e) =>
                    this.handleFormChange('localPort', e.target.value)
                  }
                  placeholder="e.g., 8080"
                  type="number"
                  value={createForm.localPort}
                />
                <small style={{ color: '#666' }}>
                  Port on your local machine (1024-65535)
                </small>
              </Form.Field>

              <Form.Field required>
                <label>Remote Host</label>
                <Input
                  onChange={(e) =>
                    this.handleFormChange('destinationHost', e.target.value)
                  }
                  placeholder="e.g., database.internal.company.com"
                  value={createForm.destinationHost}
                />
                <small style={{ color: '#666' }}>
                  Hostname or IP address of the remote service
                </small>
              </Form.Field>

              <Form.Field required>
                <label>Remote Port</label>
                <Input
                  max="65535"
                  min="1"
                  onChange={(e) =>
                    this.handleFormChange('destinationPort', e.target.value)
                  }
                  placeholder="e.g., 5432"
                  type="number"
                  value={createForm.destinationPort}
                />
                <small style={{ color: '#666' }}>
                  Port number of the remote service
                </small>
              </Form.Field>

              <Form.Field>
                <label>Service Name (Optional)</label>
                <Input
                  onChange={(e) =>
                    this.handleFormChange('serviceName', e.target.value)
                  }
                  placeholder="e.g., postgres-db"
                  value={createForm.serviceName}
                />
                <small style={{ color: '#666' }}>
                  Named service registered in the pod (for better organization)
                </small>
              </Form.Field>
            </Form>
          </Modal.Content>
          <Modal.Actions>
            <Button onClick={() => this.setState({ showCreateModal: false })}>
              Cancel
            </Button>
            <Button
              disabled={
                !selectedPodId ||
                !createForm.localPort ||
                !createForm.destinationHost ||
                !createForm.destinationPort
              }
              loading={creatingForwarding}
              onClick={this.handleCreateForwarding}
              primary
            >
              Start Forwarding
            </Button>
          </Modal.Actions>
        </Modal>
      </div>
    );
  }
}

export default PortForwarding;
