import { urlBase } from '../../config';
import * as pods from '../../lib/pods';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import PortForwarding from './PortForwarding';
import VpnGatewayConfig from './VpnGatewayConfig';
import React, { Component } from 'react';
import { withRouter } from 'react-router-dom';
import {
  Button,
  Card,
  Dimmer,
  Icon,
  Input,
  List,
  Loader,
  Segment,
  Tab,
} from 'semantic-ui-react';

const initialState = {
  activeChannelId: null,
  activePodId: null,
  intervals: {
    messages: undefined,
    pods: undefined,
  },
  loading: false,
  members: [],
  messageInput: '',
  messages: {},
  podDetail: null,
  pods: [],
};

class Pods extends Component {
  constructor(props) {
    super(props);

    this.state = initialState;
  }

  componentDidMount() {
    const podId = this.props.match?.params?.podId;
    const channelId = this.props.match?.params?.channelId;

    this.setState(
      {
        activeChannelId: channelId || null,
        activePodId: podId || null,
        intervals: {
          messages: window.setInterval(this.fetchMessages, 2_000),
          pods: window.setInterval(this.fetchPods, 5_000),
        },
      },
      async () => {
        await this.fetchPods();
        if (podId) {
          await this.selectPod(podId, channelId);
        } else if (this.state.pods.length > 0) {
          // Auto-select first pod
          await this.selectPod(this.state.pods[0].podId, null);
        }
      },
    );
  }

  componentDidUpdate(previousProps) {
    // Handle route changes
    const podId = this.props.match?.params?.podId;
    const channelId = this.props.match?.params?.channelId;
    const previousPodId = previousProps.match?.params?.podId;
    const previousChannelId = previousProps.match?.params?.channelId;

    if ((podId !== previousPodId || channelId !== previousChannelId) && podId) {
      this.selectPod(podId, channelId);
    }
  }

  componentWillUnmount() {
    const { messages: messagesInterval, pods: podsInterval } =
      this.state.intervals;

    clearInterval(podsInterval);
    clearInterval(messagesInterval);

    this.setState({ intervals: initialState.intervals });
  }

  fetchPods = async () => {
    try {
      const podsList = await pods.list();
      this.setState({ pods: podsList || [] });
    } catch (error) {
      console.error('Failed to fetch pods:', error);
      this.setState({ pods: [] });
    }
  };

  fetchPodDetail = async (podId) => {
    try {
      const detail = await pods.get(podId);
      const members = await pods.getMembers(podId);
      this.setState({ members: members || [], podDetail: detail });
    } catch (error) {
      console.error('Failed to fetch pod detail:', error);
    }
  };

  fetchMessages = async () => {
    const { activeChannelId, activePodId, messages } = this.state;

    if (!activePodId || !activeChannelId) {
      return;
    }

    try {
      const channelMessages = await pods.getMessages(
        activePodId,
        activeChannelId,
      );
      this.setState({
        messages: {
          ...messages,
          [`${activePodId}:${activeChannelId}`]: channelMessages || [],
        },
      });
    } catch (error) {
      console.error('Failed to fetch messages:', error);
    }
  };

  selectPod = async (podId, channelId = null) => {
    // Avoid redundant updates
    if (
      this.state.activePodId === podId &&
      this.state.activeChannelId === channelId
    ) {
      return;
    }

    this.setState({ activePodId: podId, loading: true });

    await this.fetchPodDetail(podId);

    // Select first channel if none specified
    const podDetail = this.state.podDetail || (await pods.get(podId));
    if (!channelId && podDetail?.channels?.length > 0) {
      channelId = podDetail.channels[0].channelId;
    }

    this.setState({
      activeChannelId: channelId,
      loading: false,
    });

    // Update URL only if different from current route
    const currentPodId = this.props.match?.params?.podId;
    const currentChannelId = this.props.match?.params?.channelId;
    if (podId !== currentPodId || channelId !== currentChannelId) {
      if (channelId) {
        this.props.history.push(
          `${urlBase}/pods/${podId}/channels/${channelId}`,
        );
      } else {
        this.props.history.push(`${urlBase}/pods/${podId}`);
      }
    }

    // Fetch messages for selected channel
    if (channelId) {
      await this.fetchMessages();
    }
  };

  handleSendMessage = async () => {
    const { activeChannelId, activePodId, messageInput } = this.state;
    const { state: applicationState } = this.props;

    if (!activePodId || !activeChannelId || !messageInput.trim()) {
      return;
    }

    // Get peerId from application state (username)
    const senderPeerId = applicationState?.user?.username || 'local-peer';

    try {
      await pods.sendMessage(
        activePodId,
        activeChannelId,
        messageInput,
        senderPeerId,
      );
      this.setState({ messageInput: '' });
      // Messages will be refreshed by interval
    } catch (error) {
      console.error('Failed to send message:', error);
      alert(`Failed to send message: ${error.message}`);
    }
  };

  handleCreatePod = async () => {
    const name = prompt('Enter pod name:');
    if (!name) return;

    try {
      const newPod = await pods.create({
        channels: [
          {
            channelId: 'general',
            kind: 'General',
            name: 'General',
          },
        ],
        externalBindings: [],
        name,
        tags: [],
        visibility: 'Unlisted',
      });

      await this.fetchPods();
      await this.selectPod(newPod.podId);
    } catch (error) {
      console.error('Failed to create pod:', error);
      alert(`Failed to create pod: ${error.message}`);
    }
  };

  render() {
    const {
      activeChannelId,
      activePodId,
      loading,
      members,
      messageInput,
      messages,
      podDetail,
      pods: podsList,
    } = this.state;

    const currentMessages =
      activePodId && activeChannelId
        ? messages[`${activePodId}:${activeChannelId}`] || []
        : [];

    const panes =
      podDetail?.channels?.map((channel) => ({
        menuItem: channel.name,
        render: () => (
          <Tab.Pane>
            <div
              style={{
                display: 'flex',
                flexDirection: 'column',
                height: '100%',
              }}
            >
              <Segment
                style={{
                  flex: 1,
                  maxHeight: '600px',
                  minHeight: '400px',
                  overflowY: 'auto',
                }}
              >
                {currentMessages.length === 0 ? (
                  <PlaceholderSegment
                    caption="No messages yet"
                    icon="comments"
                  />
                ) : (
                  <List relaxed="very">
                    {currentMessages.map((message, index) => (
                      <List.Item key={index}>
                        <List.Content>
                          <List.Header>
                            {message.senderPeerId}
                            <span
                              style={{
                                color: '#999',
                                fontSize: '0.8em',
                                marginLeft: '10px',
                              }}
                            >
                              {new Date(
                                message.timestampUnixMs,
                              ).toLocaleTimeString()}
                            </span>
                          </List.Header>
                          <List.Description>{message.body}</List.Description>
                        </List.Content>
                      </List.Item>
                    ))}
                  </List>
                )}
              </Segment>
              <Segment>
                <Input
                  action={
                    <Button
                      icon="send"
                      onClick={this.handleSendMessage}
                      primary
                    />
                  }
                  fluid
                  onChange={(e) =>
                    this.setState({ messageInput: e.target.value })
                  }
                  onKeyPress={(e) => {
                    if (e.key === 'Enter') {
                      this.handleSendMessage();
                    }
                  }}
                  placeholder="Type a message..."
                  value={messageInput}
                />
              </Segment>
            </div>
          </Tab.Pane>
        ),
      })) || [];

    return (
      <div style={{ display: 'flex', height: '100vh' }}>
        {/* Pod List Sidebar */}
        <Segment
          style={{
            borderRadius: 0,
            margin: 0,
            minWidth: '250px',
            overflowY: 'auto',
            width: '250px',
          }}
        >
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              marginBottom: '10px',
            }}
          >
            <h3>Pods</h3>
            <Button
              icon="plus"
              onClick={this.handleCreatePod}
              size="small"
            />
          </div>
          {podsList.length === 0 ? (
            <PlaceholderSegment
              caption="No pods yet"
              icon="users"
            />
          ) : (
            <List selection>
              {podsList.map((pod) => (
                <List.Item
                  active={pod.podId === activePodId}
                  key={pod.podId}
                  onClick={() => this.selectPod(pod.podId)}
                >
                  <List.Content>
                    <List.Header>{pod.name || pod.podId}</List.Header>
                    <List.Description>
                      {pod.tags?.join(', ') || 'No tags'}
                    </List.Description>
                  </List.Content>
                </List.Item>
              ))}
            </List>
          )}
        </Segment>

        {/* Pod Detail */}
        <Segment
          style={{
            borderRadius: 0,
            display: 'flex',
            flex: 1,
            flexDirection: 'column',
            margin: 0,
          }}
        >
          {loading ? (
            <Dimmer active>
              <Loader />
            </Dimmer>
          ) : !podDetail ? (
            <PlaceholderSegment
              caption="Select a pod to view details"
              icon="users"
            />
          ) : (
            <>
              <div style={{ marginBottom: '20px' }}>
                <h2>{podDetail.name || podDetail.podId}</h2>
                <p>
                  <strong>Members:</strong> {members.length}
                  {' | '}
                  <strong>Channels:</strong> {podDetail.channels?.length || 0}
                </p>
              </div>

              {podDetail.channels?.length > 0 ? (
                <Tab
                  menu={{ pointing: true }}
                  panes={[
                    ...panes,
                    {
                      menuItem: {
                        content: 'VPN Gateway',
                        icon: 'shield',
                        key: 'vpn-gateway',
                      },
                      render: () => (
                        <Tab.Pane>
                          <VpnGatewayConfig
                            podDetail={podDetail}
                            podId={activePodId}
                          />
                        </Tab.Pane>
                      ),
                    },
                    {
                      menuItem: {
                        content: 'Port Forwarding',
                        icon: 'exchange',
                        key: 'port-forwarding',
                      },
                      render: () => (
                        <Tab.Pane>
                          <PortForwarding />
                        </Tab.Pane>
                      ),
                    },
                  ]}
                />
              ) : (
                <PlaceholderSegment
                  caption="No channels available"
                  icon="comments"
                />
              )}
            </>
          )}
        </Segment>
      </div>
    );
  }
}

export default withRouter(Pods);
