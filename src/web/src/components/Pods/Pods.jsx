import { urlBase } from '../../config';
import * as pods from '../../lib/pods';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
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
  activePodId: null,
  activeChannelId: null,
  pods: [],
  loading: false,
  podDetail: null,
  members: [],
  messages: {},
  messageInput: '',
  intervals: {
    messages: undefined,
    pods: undefined,
  },
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
        activePodId: podId || null,
        activeChannelId: channelId || null,
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

  componentDidUpdate(prevProps) {
    // Handle route changes
    const podId = this.props.match?.params?.podId;
    const channelId = this.props.match?.params?.channelId;
    const prevPodId = prevProps.match?.params?.podId;
    const prevChannelId = prevProps.match?.params?.channelId;

    if (podId !== prevPodId || channelId !== prevChannelId) {
      if (podId) {
        this.selectPod(podId, channelId);
      }
    }
  }

  componentWillUnmount() {
    const { messages: messagesInterval, pods: podsInterval } = this.state.intervals;

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
      this.setState({ podDetail: detail, members: members || [] });
    } catch (error) {
      console.error('Failed to fetch pod detail:', error);
    }
  };

  fetchMessages = async () => {
    const { activePodId, activeChannelId, messages } = this.state;

    if (!activePodId || !activeChannelId) {
      return;
    }

    try {
      const channelMessages = await pods.getMessages(activePodId, activeChannelId);
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
    if (this.state.activePodId === podId && this.state.activeChannelId === channelId) {
      return;
    }

    this.setState({ loading: true, activePodId: podId });

    await this.fetchPodDetail(podId);

    // Select first channel if none specified
    const podDetail = this.state.podDetail || await pods.get(podId);
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
        this.props.history.push(`${urlBase}/pods/${podId}/channels/${channelId}`);
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
    const { activePodId, activeChannelId, messageInput } = this.state;
    const { state: applicationState } = this.props;

    if (!activePodId || !activeChannelId || !messageInput.trim()) {
      return;
    }

    // Get peerId from application state (username)
    const senderPeerId = applicationState?.user?.username || 'local-peer';

    try {
      await pods.sendMessage(activePodId, activeChannelId, messageInput, senderPeerId);
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
        name,
        visibility: 'Unlisted',
        tags: [],
        channels: [
          {
            channelId: 'general',
            kind: 'General',
            name: 'General',
          },
        ],
        externalBindings: [],
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
      pods: podsList,
      activePodId,
      activeChannelId,
      podDetail,
      members,
      messages,
      messageInput,
      loading,
    } = this.state;

    const currentMessages =
      activePodId && activeChannelId
        ? messages[`${activePodId}:${activeChannelId}`] || []
        : [];

    const panes = podDetail?.channels?.map((channel) => ({
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
                overflowY: 'auto',
                minHeight: '400px',
                maxHeight: '600px',
              }}
            >
              {currentMessages.length === 0 ? (
                <PlaceholderSegment
                  icon="comments"
                  caption="No messages yet"
                />
              ) : (
                <List relaxed="very">
                  {currentMessages.map((msg, idx) => (
                    <List.Item key={idx}>
                      <List.Content>
                        <List.Header>
                          {msg.senderPeerId}
                          <span
                            style={{
                              marginLeft: '10px',
                              fontSize: '0.8em',
                              color: '#999',
                            }}
                          >
                            {new Date(msg.timestampUnixMs).toLocaleTimeString()}
                          </span>
                        </List.Header>
                        <List.Description>{msg.body}</List.Description>
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
                onChange={(e) => this.setState({ messageInput: e.target.value })}
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
            width: '250px',
            minWidth: '250px',
            margin: 0,
            borderRadius: 0,
            overflowY: 'auto',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '10px' }}>
            <h3>Pods</h3>
            <Button
              icon="plus"
              onClick={this.handleCreatePod}
              size="small"
            />
          </div>
          {podsList.length === 0 ? (
            <PlaceholderSegment
              icon="users"
              caption="No pods yet"
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
            flex: 1,
            margin: 0,
            borderRadius: 0,
            display: 'flex',
            flexDirection: 'column',
          }}
        >
          {loading ? (
            <Dimmer active>
              <Loader />
            </Dimmer>
          ) : !podDetail ? (
            <PlaceholderSegment
              icon="users"
              caption="Select a pod to view details"
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
                  panes={panes}
                />
              ) : (
                <PlaceholderSegment
                  icon="comments"
                  caption="No channels available"
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
















