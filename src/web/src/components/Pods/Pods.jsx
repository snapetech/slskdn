import './Pods.css';
import { urlBase } from '../../config';
import * as pods from '../../lib/pods';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import PodListenAlongPanel from '../Player/PodListenAlongPanel';
import PortForwarding from './PortForwarding';
import VpnGatewayConfig from './VpnGatewayConfig';
import React, { Component } from 'react';
import { toast } from 'react-toastify';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import {
  Button,
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
  Tab,
} from 'semantic-ui-react';

const initialState = {
  activeChannelId: null,
  activeDetailTab: 0,
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
  createModalOpen: false,
  createDescription: '',
  createName: '',
  createTags: '',
  createVisibility: 'Unlisted',
  discoveryLoading: false,
  discoveryQuery: '',
  discoveryResults: [],
};

const GOLD_STAR_CLUB_POD_ID = 'pod:901d57a2c1bb4e5d90d57a2c1bb4e5d0';

const withRouter = (WrappedComponent) => {
  const RoutedComponent = (props) => {
    const location = useLocation();
    const navigate = useNavigate();
    const params = useParams();

    return (
      <WrappedComponent
        {...props}
        location={location}
        navigate={navigate}
        params={params}
      />
    );
  };

  RoutedComponent.displayName = `withRouter(${WrappedComponent.displayName || WrappedComponent.name || 'Component'})`;

  return RoutedComponent;
};

class Pods extends Component {
  constructor(props) {
    super(props);

    this.state = initialState;
  }

  componentDidMount() {
    const podId = this.props.params?.podId;
    const channelId = this.props.params?.channelId;

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
          const preferredPod =
            this.state.pods.find((pod) => pod.podId === GOLD_STAR_CLUB_POD_ID) ||
            this.state.pods[0];
          await this.selectPod(preferredPod.podId, null);
        }
      },
    );
  }

  componentDidUpdate(previousProps) {
    // Handle route changes
    const podId = this.props.params?.podId;
    const channelId = this.props.params?.channelId;
    const previousPodId = previousProps.params?.podId;
    const previousChannelId = previousProps.params?.channelId;

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

  getLocalPeerId = () => {
    return this.props.state?.user?.username || 'local-peer';
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

  getChannelIndex = (podDetail, channelId) =>
    Math.max(
      0,
      podDetail?.channels?.findIndex((channel) => channel.channelId === channelId) ??
        0,
    );

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
      activeDetailTab: this.getChannelIndex(podDetail, channelId),
      loading: false,
    });

    // Update URL only if different from current route
    const currentPodId = this.props.params?.podId;
    const currentChannelId = this.props.params?.channelId;
    if (podId !== currentPodId || channelId !== currentChannelId) {
      if (channelId) {
        this.props.navigate(`${urlBase}/pods/${podId}/channels/${channelId}`);
      } else {
        this.props.navigate(`${urlBase}/pods/${podId}`);
      }
    }

    // Fetch messages for selected channel
    if (channelId) {
      await this.fetchMessages();
    }
  };

  handleDetailTabChange = (_event, { activeIndex }) => {
    const { activePodId, podDetail } = this.state;
    const channel = podDetail?.channels?.[activeIndex];

    this.setState({ activeDetailTab: activeIndex });

    if (!channel || !activePodId) {
      return;
    }

    this.setState({ activeChannelId: channel.channelId }, async () => {
      const currentPodId = this.props.params?.podId;
      const currentChannelId = this.props.params?.channelId;

      if (
        activePodId !== currentPodId ||
        channel.channelId !== currentChannelId
      ) {
        this.props.navigate(
          `${urlBase}/pods/${activePodId}/channels/${channel.channelId}`,
        );
      }

      await this.fetchMessages();
    });
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
      toast.error(`Failed to send message: ${error.message}`);
    }
  };

  handleOpenCreatePod = () => {
    this.setState({
      createDescription: '',
      createModalOpen: true,
      createName: '',
      createTags: '',
      createVisibility: 'Unlisted',
    });
  };

  handleCreatePod = async () => {
    const {
      createDescription,
      createName,
      createTags,
      createVisibility,
    } = this.state;
    const name = createName.trim();
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
        description: createDescription.trim() || null,
        externalBindings: [],
        name,
        tags: createTags
          .split(',')
          .map((tag) => tag.trim())
          .filter(Boolean),
        visibility: createVisibility,
      }, this.getLocalPeerId());

      this.setState({ createModalOpen: false });
      await this.fetchPods();
      await this.selectPod(newPod.podId);
    } catch (error) {
      console.error('Failed to create pod:', error);
      toast.error(`Failed to create pod: ${error.message}`);
    }
  };

  handleDiscoverPods = async () => {
    const query = this.state.discoveryQuery.trim();
    this.setState({ discoveryLoading: true });

    try {
      const discovered = query
        ? await pods.discoverByName(query)
        : await pods.discoverAll(50);
      this.setState({ discoveryResults: discovered || [] });
    } catch (error) {
      console.error('Failed to discover pods:', error);
      toast.error(`Failed to discover pods: ${error.message}`);
    } finally {
      this.setState({ discoveryLoading: false });
    }
  };

  handleSaveDiscoveredPod = async (pod) => {
    const podId = pod.podId || pod.PodId;
    const name = pod.name || pod.Name || podId;
    const tags = pod.tags || pod.Tags || [];
    const visibility = pod.visibility || pod.Visibility || 'Unlisted';
    const focusContentId = pod.focusContentId || pod.FocusContentId || null;

    if (!podId) return;

    try {
      const savedPod = await pods.create({
        channels: [
          {
            channelId: 'general',
            kind: 'General',
            name: 'General',
          },
        ],
        externalBindings: [],
        focusContentId,
        name,
        podId,
        tags,
        visibility,
      }, this.getLocalPeerId());

      toast.success(`Saved pod ${name}`);
      await this.fetchPods();
      await this.selectPod(savedPod.podId);
    } catch (error) {
      console.error('Failed to save discovered pod:', error);
      toast.error(`Failed to save pod: ${error.message}`);
    }
  };

  handleLeaveActivePod = async () => {
    const { activePodId, podDetail } = this.state;
    const peerId = this.getLocalPeerId();
    const podName = podDetail?.name || activePodId;

    if (!activePodId || !peerId) return;

    if (
      podDetail?.podId === 'pod:901d57a2c1bb4e5d90d57a2c1bb4e5d0' &&
      !window.confirm(
        'Leaving Gold Star Club is irrevocable. You will not be able to rejoin or recover Gold Star status later. Leave anyway?',
      )
    ) {
      return;
    }

    try {
      await pods.leave(activePodId, peerId);
      toast.success(`Left ${podName}`);
      await this.fetchPodDetail(activePodId);
    } catch (error) {
      console.error('Failed to leave pod:', error);
      toast.error(`Failed to leave pod: ${error.message}`);
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
      createDescription,
      createModalOpen,
      createName,
      createTags,
      createVisibility,
      discoveryLoading,
      discoveryQuery,
      discoveryResults,
    } = this.state;

    const currentMessages =
      activePodId && activeChannelId
        ? messages[`${activePodId}:${activeChannelId}`] || []
        : [];
    const localPeerId = this.getLocalPeerId();
    const isMember = members.some((member) => member.peerId === localPeerId);
    const isGoldStarClub = podDetail?.podId === GOLD_STAR_CLUB_POD_ID;
    const activeChannel = podDetail?.channels?.find(
      (channel) => channel.channelId === activeChannelId,
    );

    return (
      <div className="pods-workspace">
        {/* Pod List Sidebar */}
        <Segment className="pods-sidebar">
          <div className="pods-sidebar-header">
            <h3>Pods</h3>
            <Popup
              content="Create a durable pod with a default channel. It is saved by the daemon and restored after restart."
              trigger={
                <Button
                  icon="plus"
                  onClick={this.handleOpenCreatePod}
                  size="small"
                />
              }
            />
          </div>
          <Input
            action={
              <Popup
                content="Find listed pods through the pod discovery index."
                trigger={
                  <Button
                    icon="search"
                    loading={discoveryLoading}
                    onClick={this.handleDiscoverPods}
                  />
                }
              />
            }
            fluid
            onChange={(e) =>
              this.setState({ discoveryQuery: e.target.value })
            }
            onKeyUp={(e) => {
              if (e.key === 'Enter') {
                this.handleDiscoverPods();
              }
            }}
            placeholder="Find pods..."
            size="small"
            value={discoveryQuery}
          />
          {discoveryResults.length > 0 && (
            <Segment className="pod-discovery-results">
              <Header
                as="h5"
                dividing
              >
                Discovered
              </Header>
              <List selection>
                {discoveryResults.slice(0, 6).map((pod) => {
                  const podId = pod.podId || pod.PodId;
                  const name = pod.name || pod.Name || podId;
                  const tags = pod.tags || pod.Tags || [];
                  const local = podsList.some((item) => item.podId === podId);

                  return (
                    <List.Item
                      key={podId}
                      onClick={() => local && this.selectPod(podId)}
                    >
                      <List.Content>
                        <List.Header>
                          {name}
                          {local && (
                            <Label
                              color="green"
                              size="mini"
                            >
                              saved
                            </Label>
                          )}
                        </List.Header>
                        <List.Description>
                          {tags.length > 0 ? tags.join(', ') : podId}
                        </List.Description>
                      </List.Content>
                      {!local && (
                        <List.Content floated="right">
                          <Popup
                            content="Save this discovered pod locally so it appears in your pod list after restarts."
                            trigger={
                              <Button
                                basic
                                icon="save"
                                onClick={(event) => {
                                  event.stopPropagation();
                                  this.handleSaveDiscoveredPod(pod);
                                }}
                                size="mini"
                              />
                            }
                          />
                        </List.Content>
                      )}
                    </List.Item>
                  );
                })}
              </List>
            </Segment>
          )}
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
        <Segment className="pod-detail">
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
              <div className="pod-detail-header">
                <h2>{podDetail.name || podDetail.podId}</h2>
                <div className="pod-detail-meta">
                  <span>
                    <strong>Members:</strong> {members.length}
                  </span>
                  <span>
                    <strong>Channels:</strong> {podDetail.channels?.length || 0}
                  </span>
                  <span>
                    <strong>Visibility:</strong> {podDetail.visibility}
                  </span>
                </div>
                {podDetail.description && <p>{podDetail.description}</p>}
                {isGoldStarClub && (
                  <Message warning>
                    <Icon name="star" />
                    Gold Star Club membership is limited to the first 250 nodes. Leaving this pod permanently revokes local Gold Star status. There are no rejoins.
                  </Message>
                )}
                {podDetail.tags?.length > 0 && (
                  <div className="pod-tag-list">
                    {podDetail.tags.map((tag) => (
                      <Label
                        key={tag}
                        size="small"
                      >
                        {tag}
                      </Label>
                    ))}
                  </div>
                )}
                {isMember && (
                  <Popup
                    content={
                      isGoldStarClub
                        ? 'Permanently leave Gold Star Club. This is irrevocable and cannot be undone.'
                        : 'Leave this pod with the current user.'
                    }
                    trigger={
                      <Button
                        icon
                        labelPosition="left"
                        negative={isGoldStarClub}
                        onClick={this.handleLeaveActivePod}
                        size="small"
                      >
                        <Icon name="sign-out" />
                        {isGoldStarClub ? 'Revoke Gold Star' : 'Leave Pod'}
                      </Button>
                    }
                  />
                )}
              </div>
              {activeChannelId && activeChannel?.kind !== 'Direct' && (
                <PodListenAlongPanel
                  channelId={activeChannelId}
                  compact
                  podId={activePodId}
                  user={this.props.state?.user?.username}
                />
              )}

              {podDetail.channels?.length > 0 ? (
                <>
                  <div className="pod-channel-selector">
                    {podDetail.channels.map((channel, index) => (
                      <Button
                        active={channel.channelId === activeChannelId}
                        icon={channel.kind === 'Direct' ? 'comments' : 'comment alternate'}
                        key={channel.channelId}
                        labelPosition="left"
                        onClick={() =>
                          this.handleDetailTabChange(null, { activeIndex: index })
                        }
                        size="small"
                      >
                        {channel.name || channel.channelId}
                      </Button>
                    ))}
                  </div>
                  <Segment className="pod-channel-chat">
                    <div className="pod-channel-heading">
                      <div>
                        <Header
                          as="h3"
                          content={activeChannel?.name || activeChannelId}
                        />
                        <span className="pod-channel-subtitle">
                          {activeChannel?.kind === 'Direct'
                            ? 'Direct pod channel'
                            : `${activeChannel?.kind || 'Pod'} channel`}
                        </span>
                      </div>
                      <Label basic>
                        {currentMessages.length}{' '}
                        {currentMessages.length === 1 ? 'message' : 'messages'}
                      </Label>
                    </div>
                    <Segment className="pod-message-history">
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
                    <Segment className="pod-message-composer">
                      <Input
                        action={
                          <Popup
                            content="Send this message to the active pod channel."
                            trigger={
                              <Button
                                icon="send"
                                onClick={this.handleSendMessage}
                                primary
                              />
                            }
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
                  </Segment>
                  <Tab
                    menu={{ pointing: true }}
                    panes={[
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
                    renderActiveOnly={false}
                  />
                </>
              ) : (
                <PlaceholderSegment
                  caption="No channels available"
                  icon="comments"
                />
              )}
            </>
          )}
        </Segment>
        <Modal
          onClose={() => this.setState({ createModalOpen: false })}
          open={createModalOpen}
          size="small"
        >
          <Modal.Header>Create Pod</Modal.Header>
          <Modal.Content>
            <Form>
              <Form.Field>
                <label>Name</label>
                <Input
                  autoFocus
                  onChange={(e) =>
                    this.setState({ createName: e.target.value })
                  }
                  placeholder="listening circle, label crate, private crew"
                  value={createName}
                />
              </Form.Field>
              <Form.TextArea
                label="Description"
                onChange={(e, { value }) =>
                  this.setState({ createDescription: value })
                }
                placeholder="What this pod is for"
                value={createDescription}
              />
              <Form.Field>
                <label>Tags</label>
                <Input
                  onChange={(e) =>
                    this.setState({ createTags: e.target.value })
                  }
                  placeholder="ambient, friends, vinyl"
                  value={createTags}
                />
              </Form.Field>
              <Form.Field>
                <label>Visibility</label>
                <Dropdown
                  fluid
                  onChange={(e, { value }) =>
                    this.setState({ createVisibility: value })
                  }
                  options={[
                    { key: 'unlisted', text: 'Unlisted', value: 'Unlisted' },
                    { key: 'listed', text: 'Listed', value: 'Listed' },
                    { key: 'private', text: 'Private', value: 'Private' },
                  ]}
                  selection
                  value={createVisibility}
                />
              </Form.Field>
              <Message info>
                <Icon name="save" />
                Pods are stored by the server, so the list and messages survive browser reloads and daemon restarts.
              </Message>
            </Form>
          </Modal.Content>
          <Modal.Actions>
            <Button onClick={() => this.setState({ createModalOpen: false })}>
              Cancel
            </Button>
            <Button
              disabled={!createName.trim()}
              onClick={this.handleCreatePod}
              primary
            >
              Create
            </Button>
          </Modal.Actions>
        </Modal>
      </div>
    );
  }
}

export default withRouter(Pods);
