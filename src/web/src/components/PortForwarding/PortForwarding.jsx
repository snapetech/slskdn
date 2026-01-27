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
  Header,
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
  createForm: {
    destinationHost: '',
    destinationPort: '',
    localPort: '',
    serviceName: '',
  },
  creatingForwarding: false,
  error: null,
  forwardingStatus: [],
  intervals: {
    status: undefined,
  },
  loading: false,
  pods: [],
  selectedPodDetail: null,
  selectedPodId: null,
  showCreateModal: false,
  stoppingForwarding: false,
};

class PortForwarding extends Component {
  constructor(props) {
    super(props);
    this.state = initialState;
  }

  componentDidMount() {
    this.setState({
      intervals: {
        status: window.setInterval(this.fetchForwardingStatus, 5_000),
      },
    });

    this.initializeComponent();
  }

  componentWillUnmount() {
    const { status } = this.state.intervals;
    clearInterval(status);
    this.setState({ intervals: initialState.intervals });
  }

  initializeComponent = async () => {
    this.setState({ error: null, loading: true });

    try {
      await Promise.all([
        this.fetchPods(),
        this.fetchAvailablePorts(),
        this.fetchForwardingStatus(),
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
    this.setState({ error: null, stoppingForwarding: true });

    try {
      await portForwarding.stopForwarding(localPort);
      await Promise.all([
        this.fetchAvailablePorts(),
        this.fetchForwardingStatus(),
      ]);
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
            <div style={{ marginBottom: '20px' }}>
              <Header as="h3">Active Port Forwarding</Header>
              <p>Monitor and manage your active VPN tunnel connections.</p>
            </div>

            {forwardingStatus.length === 0 ? (
              <Segment placeholder>
                <Icon
                  name="exchange"
                  size="huge"
                />
                <h3>No active port forwarding</h3>
                <p>
                  Start forwarding local ports to remote services through VPN
                  tunnels.
                </p>
                <Button
                  disabled={vpnCapablePods.length === 0}
                  onClick={() => this.setState({ showCreateModal: true })}
                  primary
                  size="large"
                >
                  <Icon name="plus" />
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
                          <Label color="blue">
                            <Icon name="desktop" />
                            localhost:{forwarding.localPort}
                          </Label>
                        </Table.Cell>
                        <Table.Cell>
                          <div>
                            <strong>{forwarding.podId}</strong>
                            {forwarding.serviceName && (
                              <div
                                style={{
                                  color: '#666',
                                  fontSize: '0.8em',
                                  marginTop: '4px',
                                }}
                              >
                                <Icon name="server" />
                                Service: {forwarding.serviceName}
                              </div>
                            )}
                          </div>
                        </Table.Cell>
                        <Table.Cell>
                          <code
                            style={{
                              backgroundColor: '#f8f9fa',
                              borderRadius: '4px',
                              padding: '4px 8px',
                            }}
                          >
                            {forwarding.destinationHost}:
                            {forwarding.destinationPort}
                          </code>
                        </Table.Cell>
                        <Table.Cell>
                          <Label color={forwarding.isActive ? 'green' : 'red'}>
                            <Icon
                              name={
                                forwarding.isActive ? 'checkmark' : 'remove'
                              }
                            />
                            {forwarding.isActive ? 'Active' : 'Inactive'}
                          </Label>
                        </Table.Cell>
                        <Table.Cell textAlign="center">
                          <Label
                            circular
                            color="blue"
                          >
                            {forwarding.activeConnections}
                          </Label>
                        </Table.Cell>
                        <Table.Cell>
                          {forwarding.bytesForwarded > 0 ? (
                            <span>
                              {(forwarding.bytesForwarded / 1_024).toFixed(1)}{' '}
                              KB
                            </span>
                          ) : (
                            <span style={{ color: '#999' }}>0 KB</span>
                          )}
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
              <Header as="h3">Available Ports</Header>
              <p>Check which local ports are available for forwarding.</p>
            </div>

            <Statistic.Group
              size="small"
              widths="three"
            >
              <Statistic color="green">
                <Statistic.Value>{availablePorts.length}</Statistic.Value>
                <Statistic.Label>Available Ports</Statistic.Label>
              </Statistic>
              <Statistic color="blue">
                <Statistic.Value>{forwardingStatus.length}</Statistic.Value>
                <Statistic.Label>In Use</Statistic.Label>
              </Statistic>
              <Statistic color="grey">
                <Statistic.Value>
                  {65_535 -
                    1_024 +
                    1 -
                    availablePorts.length -
                    forwardingStatus.length}
                </Statistic.Value>
                <Statistic.Label>System Reserved</Statistic.Label>
              </Statistic>
            </Statistic.Group>

            <Segment style={{ marginTop: '20px' }}>
              <Header as="h4">Available Ports for Forwarding</Header>
              <p>Ports in range 1024-65535 that are currently available:</p>
              <div
                style={{
                  backgroundColor: '#f8f9fa',
                  border: '1px solid #dee2e6',
                  borderRadius: '4px',
                  fontFamily: 'monospace',
                  fontSize: '12px',
                  maxHeight: '300px',
                  overflowY: 'auto',
                  padding: '15px',
                }}
              >
                {availablePorts.length > 0 ? (
                  <div>
                    {availablePorts.slice(0, 50).join(', ')}
                    {availablePorts.length > 50 && (
                      <div style={{ color: '#666', marginTop: '10px' }}>
                        ... and {availablePorts.length - 50} more ports
                        available
                      </div>
                    )}
                  </div>
                ) : (
                  <em style={{ color: '#999' }}>
                    No ports available or still loading...
                  </em>
                )}
              </div>
            </Segment>
          </Tab.Pane>
        ),
      },
      {
        menuItem: 'VPN Pods',
        render: () => (
          <Tab.Pane>
            <div style={{ marginBottom: '20px' }}>
              <Header as="h3">VPN-Capable Pods</Header>
              <p>Pods that support VPN tunneling for port forwarding.</p>
            </div>

            {vpnCapablePods.length === 0 ? (
              <Segment placeholder>
                <Icon
                  color="orange"
                  name="warning circle"
                  size="huge"
                />
                <h3>No VPN-Capable Pods Found</h3>
                <p>
                  To use port forwarding, you need at least one pod with VPN
                  gateway capability enabled.
                </p>
                <div style={{ marginTop: '15px' }}>
                  <p>
                    <strong>How to enable VPN on a pod:</strong>
                  </p>
                  <ol>
                    <li>Create or join a pod</li>
                    <li>
                      Enable the <code>PrivateServiceGateway</code> capability
                    </li>
                    <li>Configure allowed destinations and policies</li>
                  </ol>
                </div>
              </Segment>
            ) : (
              <Card.Group itemsPerRow={2}>
                {vpnCapablePods.map((pod) => (
                  <Card
                    fluid
                    key={pod.podId}
                  >
                    <Card.Content>
                      <Card.Header>
                        <Icon
                          color="green"
                          name="shield"
                        />
                        {pod.name || pod.podId}
                      </Card.Header>
                      <Card.Meta>Pod ID: {pod.podId}</Card.Meta>
                      <Card.Description>
                        <p>
                          <strong>Members:</strong> {pod.members?.length || 0}
                        </p>
                        <p>
                          <strong>Channels:</strong> {pod.channels?.length || 0}
                        </p>
                        {pod.privateServicePolicy?.enabled && (
                          <div>
                            <Label
                              color="green"
                              size="small"
                            >
                              <Icon name="lock" />
                              VPN Gateway Active
                            </Label>
                            {pod.privateServicePolicy.allowedDestinations && (
                              <p
                                style={{ fontSize: '0.9em', marginTop: '8px' }}
                              >
                                <strong>Allowed Destinations:</strong>{' '}
                                {
                                  pod.privateServicePolicy.allowedDestinations
                                    .length
                                }
                              </p>
                            )}
                          </div>
                        )}
                      </Card.Description>
                    </Card.Content>
                    <Card.Content extra>
                      <Button
                        fluid
                        onClick={() => {
                          this.setState({ selectedPodId: pod.podId });
                          this.handlePodSelection(pod.podId);
                          this.setState({ showCreateModal: true });
                        }}
                        primary
                      >
                        <Icon name="exchange" />
                        Use for Forwarding
                      </Button>
                    </Card.Content>
                  </Card>
                ))}
              </Card.Group>
            )}
          </Tab.Pane>
        ),
      },
    ];

    return (
      <div style={{ padding: '20px' }}>
        <Dimmer active={loading}>
          <Loader size="large">Loading port forwarding...</Loader>
        </Dimmer>

        <div style={{ marginBottom: '30px' }}>
          <Header as="h1">
            <Icon name="exchange" />
            Port Forwarding
          </Header>
          <p style={{ color: '#666', fontSize: '1.1em' }}>
            Forward local ports to remote services through secure VPN tunnels.
            Access internal databases, APIs, and services as if they were
            running locally.
          </p>
        </div>

        {error && (
          <Message error>
            <Message.Header>Operation Failed</Message.Header>
            <p>{error}</p>
            <Button
              onClick={() => this.setState({ error: null })}
              size="small"
            >
              Dismiss
            </Button>
          </Message>
        )}

        <Tab
          menu={{ pointing: true, secondary: true }}
          panes={panes}
          style={{ marginTop: '20px' }}
        />

        {/* Create Forwarding Modal */}
        <Modal
          closeIcon
          onClose={() => this.setState({ showCreateModal: false })}
          open={showCreateModal}
          size="small"
        >
          <Modal.Header>
            <Icon name="plus" />
            Start Port Forwarding
          </Modal.Header>
          <Modal.Content>
            <Message info>
              <Message.Header>Secure VPN Tunneling</Message.Header>
              <p>
                Create an encrypted tunnel from your local machine to a remote
                service through a pod's VPN gateway.
              </p>
            </Message>

            <Form>
              <Form.Field required>
                <label>VPN Pod</label>
                <Dropdown
                  fluid
                  loading={loading}
                  onChange={(e, { value }) => this.handlePodSelection(value)}
                  options={vpnCapablePods.map((pod) => ({
                    description: pod.privateServicePolicy?.enabled
                      ? 'VPN Gateway Active'
                      : 'VPN Capable',
                    key: pod.podId,
                    text: `${pod.name || pod.podId} (${pod.members?.length || 0} members)`,
                    value: pod.podId,
                  }))}
                  placeholder="Select a VPN-capable pod"
                  search
                  selection
                  value={selectedPodId || ''}
                />
                {selectedPodDetail && (
                  <div
                    style={{
                      backgroundColor: '#f8f9fa',
                      borderRadius: '4px',
                      marginTop: '10px',
                      padding: '10px',
                    }}
                  >
                    <p style={{ margin: '0 0 5px 0' }}>
                      <strong>Pod:</strong>{' '}
                      {selectedPodDetail.name || selectedPodDetail.podId}
                    </p>
                    <p style={{ margin: '0 0 5px 0' }}>
                      <strong>Members:</strong>{' '}
                      {selectedPodDetail.members?.length || 0}
                    </p>
                    {selectedPodDetail.privateServicePolicy?.enabled && (
                      <p style={{ color: '#28a745', margin: '0' }}>
                        <Icon name="shield" />
                        <strong>VPN Gateway:</strong> Enabled
                      </p>
                    )}
                  </div>
                )}
              </Form.Field>

              <Form.Field required>
                <label>Local Port</label>
                <Popup
                  content="Port on your local machine where the remote service will be accessible (1024-65535)"
                  trigger={
                    <Input
                      label={{ basic: true, content: 'localhost:' }}
                      labelPosition="left"
                      max="65535"
                      min="1024"
                      onChange={(e) =>
                        this.handleFormChange('localPort', e.target.value)
                      }
                      placeholder="e.g., 8080"
                      type="number"
                      value={createForm.localPort}
                    />
                  }
                />
              </Form.Field>

              <Form.Field required>
                <label>Remote Host</label>
                <Popup
                  content="Hostname or IP address of the remote service you want to access"
                  trigger={
                    <Input
                      onChange={(e) =>
                        this.handleFormChange('destinationHost', e.target.value)
                      }
                      placeholder="e.g., database.internal.company.com"
                      value={createForm.destinationHost}
                    />
                  }
                />
              </Form.Field>

              <Form.Field required>
                <label>Remote Port</label>
                <Popup
                  content="Port number of the remote service"
                  trigger={
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
                  }
                />
              </Form.Field>

              <Form.Field>
                <label>Service Name (Optional)</label>
                <Popup
                  content="Friendly name for the service (helps organize your forwarding rules)"
                  trigger={
                    <Input
                      onChange={(e) =>
                        this.handleFormChange('serviceName', e.target.value)
                      }
                      placeholder="e.g., postgres-db, api-server"
                      value={createForm.serviceName}
                    />
                  }
                />
              </Form.Field>

              <Message warning>
                <Message.Header>Security Notice</Message.Header>
                <p>
                  All traffic will be routed through the pod's VPN gateway with
                  end-to-end encryption. The remote destination must be allowed
                  by the pod's security policy.
                </p>
              </Message>
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
              <Icon name="play" />
              Start Forwarding
            </Button>
          </Modal.Actions>
        </Modal>
      </div>
    );
  }
}

export default PortForwarding;
