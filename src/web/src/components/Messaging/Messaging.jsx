import './Messaging.css';
import * as chat from '../../lib/chat';
import * as pods from '../../lib/pods';
import * as rooms from '../../lib/rooms';
import { getLocalStorageItem, setLocalStorageItem } from '../../lib/storage';
import ChatSession from '../Chat/ChatSession';
import PodListenAlongPanel from '../Player/PodListenAlongPanel';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import RoomCreateModal from '../Rooms/RoomCreateModal';
import RoomSession from '../Rooms/RoomSession';
import UserCard from '../Shared/UserCard';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Card,
  Dropdown,
  Icon,
  Input,
  Label,
  List,
  Popup,
  Segment,
} from 'semantic-ui-react';

const STORAGE_KEY = 'slskd-messaging-workspace';
const GOLD_STAR_CLUB_POD_ID = 'pod:901d57a2c1bb4e5d90d57a2c1bb4e5d0';

let panelCounter = 0;

const loadPanels = () => {
  try {
    const saved = getLocalStorageItem(STORAGE_KEY);
    if (!saved) {
      return [];
    }

    const parsed = JSON.parse(saved);
    panelCounter = parsed.panelCounter || 0;
    return Array.isArray(parsed.panels) ? parsed.panels : [];
  } catch {
    return [];
  }
};

const savePanels = (panels) => {
  setLocalStorageItem(STORAGE_KEY, JSON.stringify({ panelCounter, panels }));
};

const makePanel = (type, target, collapsed = false) => {
  panelCounter += 1;
  return {
    collapsed,
    id: `${type}-${panelCounter}`,
    target,
    type,
  };
};

const encodePodTarget = (podId, channelId) => `${podId}\u001f${channelId}`;

const decodePodTarget = (target) => {
  const [podId, channelId] = `${target || ''}`.split('\u001f');
  return { channelId, podId };
};

const channelLabel = (channel) =>
  [channel.podName, channel.channelName || channel.channelId]
    .filter(Boolean)
    .join(' / ');

const normalizeConversationName = (value) => `${value || ''}`.trim().toLowerCase();

const isPodDirectChannel = (channel) => {
  const channelKind = normalizeConversationName(channel.channelKind);
  const channelName = normalizeConversationName(
    channel.channelName || channel.channelId,
  );

  return (
    channelKind === 'direct' ||
    channelName === 'dm' ||
    channelName === 'direct' ||
    channelName === 'direct message'
  );
};

const panelLabel = (panel) => {
  if (panel.type === 'room') return `#${panel.target}`;
  if (panel.type === 'pod') return panel.label || 'Pod channel';

  return panel.target;
};

const PodChannelSession = ({ channel, state }) => {
  const [body, setBody] = useState('');
  const [members, setMembers] = useState([]);
  const [messages, setMessages] = useState([]);

  const refresh = useCallback(async () => {
    if (!channel?.podId || !channel?.channelId) return;

    const [channelMessages, podMembers] = await Promise.all([
      pods.getMessages(channel.podId, channel.channelId),
      pods.getMembers(channel.podId).catch(() => []),
    ]);

    setMessages(channelMessages || []);
    setMembers(podMembers || []);
  }, [channel?.channelId, channel?.podId]);

  useEffect(() => {
    refresh().catch((error) => {
      console.error('Failed to load pod channel messages:', error);
    });
    const interval = window.setInterval(() => {
      refresh().catch((error) => {
        console.error('Failed to load pod channel messages:', error);
      });
    }, 2_000);

    return () => window.clearInterval(interval);
  }, [refresh]);

  const send = async () => {
    const trimmed = body.trim();
    if (!trimmed || !channel?.podId || !channel?.channelId) return;

    await pods.sendMessage(
      channel.podId,
      channel.channelId,
      trimmed,
      state?.user?.username || 'local-peer',
    );
    setBody('');
    await refresh();
  };

  return (
    <div className="pod-message-session">
      {!isPodDirectChannel(channel) && (
        <PodListenAlongPanel
          channelId={channel.channelId}
          compact
          podId={channel.podId}
          user={state?.user?.username}
        />
      )}
      <div className="pod-message-session-main">
        <Segment.Group>
          <Segment className="pod-message-session-history">
            {messages.length === 0 ? (
              <PlaceholderSegment
                caption="No messages yet"
                icon="comments"
              />
            ) : (
              <List>
                {messages.map((message, index) => (
                  <List.Content
                    className={`room-message ${message.senderPeerId === state?.user?.username ? 'room-message-self' : ''}`}
                    key={`${message.timestampUnixMs}-${index}`}
                  >
                    <span className="room-message-time">
                      {message.timestampUnixMs
                        ? new Date(message.timestampUnixMs).toLocaleTimeString()
                        : ''}
                    </span>
                    <span className="room-message-name">
                      {message.senderPeerId}:{' '}
                    </span>
                    <span className="room-message-message">
                      {message.body}
                    </span>
                  </List.Content>
                ))}
              </List>
            )}
          </Segment>
          <Segment className="pod-message-session-composer">
            <div className="messaging-start-row">
              <Input
                aria-label={`Message ${channelLabel(channel)}`}
                className="pod-message-session-input"
                fluid
                onChange={(event) => setBody(event.target.value)}
                onKeyUp={(event) => {
                  if (event.key === 'Enter') {
                    send().catch((error) => {
                      console.error('Failed to send pod message:', error);
                    });
                  }
                }}
                placeholder={`Message ${channel.channelName || channel.channelId}`}
                value={body}
              />
              <Popup
                content="Send this message to the pod channel."
                trigger={
                  <Button
                    aria-label={`Send message to ${channelLabel(channel)}`}
                    disabled={!body.trim()}
                    icon="send"
                    onClick={() =>
                      send().catch((error) => {
                        console.error('Failed to send pod message:', error);
                      })
                    }
                    primary
                    title={`Send message to ${channelLabel(channel)}`}
                  />
                }
              />
            </div>
          </Segment>
        </Segment.Group>
        <Segment className="room-users pod-message-users">
          <div className="room-users-header">
            <Icon name="users" />
            Members ({members.length})
          </div>
          <List
            divided
            relaxed
          >
            {members.map((member) => {
              const username = member.peerId || member.username || member.PeerId;

              return (
                <List.Item key={username}>
                  <List.Content>
                    <List.Header>
                      <UserCard username={username}>{username}</UserCard>
                    </List.Header>
                    <List.Description>
                      {member.role || member.Role || 'Member'}
                    </List.Description>
                  </List.Content>
                </List.Item>
              );
            })}
          </List>
        </Segment>
      </div>
    </div>
  );
};

const Messaging = ({ initialKind = 'mixed', state }) => {
  const navigate = useNavigate();
  const [panels, setPanels] = useState(() => loadPanels());
  const [chatTarget, setChatTarget] = useState('');
  const [conversations, setConversations] = useState([]);
  const [joinedRooms, setJoinedRooms] = useState([]);
  const [podChannels, setPodChannels] = useState([]);
  const [availableRooms, setAvailableRooms] = useState([]);
  const [roomSearchLoading, setRoomSearchLoading] = useState(false);

  const openPanel = useCallback((type, target, metadata = {}) => {
    const trimmed = `${target || ''}`.trim();
    if (!trimmed) {
      return;
    }

    setPanels((previous) => {
      const existing = previous.find(
        (panel) => panel.type === type && panel.target === trimmed,
      );
      if (existing) {
        return previous.map((panel) =>
          panel.id === existing.id
            ? { ...panel, ...metadata, collapsed: false }
            : panel,
        );
      }

      return [...previous, { ...makePanel(type, trimmed), ...metadata }];
    });
  }, []);

  const closePanel = useCallback((panelId) => {
    setPanels((previous) => previous.filter((panel) => panel.id !== panelId));
  }, []);

  const setPanelCollapsed = useCallback((panelId, collapsed) => {
    setPanels((previous) =>
      previous.map((panel) =>
        panel.id === panelId ? { ...panel, collapsed } : panel,
      ),
    );
  }, []);

  const hydrate = useCallback(async () => {
    const [serverConversations, serverJoinedRooms, serverPods] = await Promise.all([
      chat.getAll(),
      rooms.getJoined(),
      pods.list().catch(() => []),
    ]);
    const podDetails = await Promise.all(
      (serverPods || []).map(async (pod) => {
        try {
          return await pods.get(pod.podId);
        } catch {
          return pod;
        }
      }),
    );

    setConversations(
      (serverConversations || [])
        .filter((conversation) => conversation.username)
        .sort((a, b) => {
          if (a.hasUnAcknowledgedMessages !== b.hasUnAcknowledgedMessages) {
            return a.hasUnAcknowledgedMessages ? -1 : 1;
          }

          return a.username.localeCompare(b.username);
        }),
    );
    setJoinedRooms((serverJoinedRooms || []).filter(Boolean).sort());
    setPodChannels(
      podDetails
        .flatMap((pod) =>
          (pod.channels || []).map((channel) => ({
            channelId: channel.channelId,
            channelKind: channel.kind,
            channelName: channel.name,
            podId: pod.podId,
            podName: pod.name || pod.podId,
            target: encodePodTarget(pod.podId, channel.channelId),
          })),
        )
        .sort((a, b) => channelLabel(a).localeCompare(channelLabel(b))),
    );
  }, []);

  const savedChatNames = useMemo(
    () =>
      new Set(
        conversations.map((conversation) =>
          normalizeConversationName(conversation.username),
        ),
      ),
    [conversations],
  );

  const bridgedPodNames = useMemo(
    () =>
      new Set(
        podChannels
          .filter(
            (channel) =>
              isPodDirectChannel(channel) &&
              savedChatNames.has(normalizeConversationName(channel.podName)),
          )
          .map((channel) => normalizeConversationName(channel.podName)),
      ),
    [podChannels, savedChatNames],
  );

  const hiddenPodDirectTargets = useMemo(
    () =>
      new Set(
        podChannels
          .filter((channel) => isPodDirectChannel(channel))
          .map((channel) => channel.target),
      ),
    [podChannels],
  );

  const visiblePodChannels = useMemo(
    () =>
      podChannels.filter((channel) => !isPodDirectChannel(channel)),
    [podChannels],
  );

  useEffect(() => {
    hydrate().catch((error) => {
      console.error('Failed to hydrate messaging workspace:', error);
    });
    const interval = window.setInterval(() => {
      hydrate().catch((error) => {
        console.error('Failed to hydrate messaging workspace:', error);
      });
    }, 10_000);
    return () => window.clearInterval(interval);
  }, [hydrate]);

  useEffect(() => {
    savePanels(panels);
  }, [panels]);

  useEffect(() => {
    if (hiddenPodDirectTargets.size === 0) {
      return;
    }

    setPanels((previous) =>
      previous.filter(
        (panel) =>
          !(panel.type === 'pod' && hiddenPodDirectTargets.has(panel.target)),
      ),
    );
  }, [hiddenPodDirectTargets]);

  useEffect(() => {
    if (panels.length > 0) {
      return;
    }

    if (initialKind === 'chat' && conversations[0]?.username) {
      openPanel('chat', conversations[0].username);
    }

    if (initialKind === 'room' && joinedRooms[0]) {
      openPanel('room', joinedRooms[0]);
    }

    if (initialKind === 'pod' && visiblePodChannels[0]) {
      openPanel('pod', visiblePodChannels[0].target, {
        label: channelLabel(visiblePodChannels[0]),
      });
    }
  }, [
    conversations,
    initialKind,
    joinedRooms,
    openPanel,
    panels.length,
    visiblePodChannels,
  ]);

  const fetchAvailableRooms = async () => {
    setRoomSearchLoading(true);
    try {
      setAvailableRooms((await rooms.getAvailable()) || []);
    } catch {
      setAvailableRooms([]);
    } finally {
      setRoomSearchLoading(false);
    }
  };

  const joinRoom = async (roomName) => {
    if (!roomName) {
      return;
    }

    try {
      await rooms.join({ roomName });
      await hydrate();
      openPanel('room', roomName);
    } catch (error) {
      console.error('Failed to join room:', error);
    }
  };

  const leaveRoom = async (roomName) => {
    if (
      !window.confirm(
        `Leave room "${roomName}"? This exits the room and removes it from joined rooms.`,
      )
    ) {
      return;
    }

    try {
      await rooms.leave({ roomName });
      await hydrate();
      setPanels((previous) =>
        previous.filter(
          (panel) => !(panel.type === 'room' && panel.target === roomName),
        ),
      );
    } catch (error) {
      console.error('Failed to leave room:', error);
    }
  };

  const deleteConversation = async (username) => {
    if (
      !window.confirm(
        `Permanently delete the saved message thread with "${username}"?`,
      )
    ) {
      return;
    }

    try {
      await chat.remove({ username });
      await hydrate();
      setPanels((previous) =>
        previous.filter(
          (panel) => !(panel.type === 'chat' && panel.target === username),
        ),
      );
    } catch (error) {
      console.error('Failed to delete conversation:', error);
    }
  };

  const leavePod = async (channel) => {
    const peerId = state?.user?.username || 'local-peer';
    const podName = channel?.podName || channel?.podId;
    if (!channel?.podId || !peerId) {
      return;
    }

    const prompt =
      channel.podId === GOLD_STAR_CLUB_POD_ID
        ? `Permanently leave ${podName}? Gold Star Club membership is irrevocable and cannot be recovered.`
        : `Leave pod "${podName}"? This exits the pod and removes its channels from Messages.`;

    if (!window.confirm(prompt)) {
      return;
    }

    try {
      await pods.leave(channel.podId, peerId);
      await hydrate();
      setPanels((previous) =>
        previous.filter((panel) => {
          if (panel.type !== 'pod') return true;
          const { podId } = decodePodTarget(panel.target);
          return podId !== channel.podId;
        }),
      );
    } catch (error) {
      console.error('Failed to leave pod:', error);
    }
  };

  const roomOptions = useMemo(
    () =>
      availableRooms.map((room) => ({
        description: room.isPrivate ? 'Private' : '',
        key: room.name,
        text: `${room.name} (${room.userCount} users)`,
        value: room.name,
      })),
    [availableRooms],
  );

  const openPanels = panels.filter((panel) => !panel.collapsed);
  const collapsedPanels = panels.filter((panel) => panel.collapsed);

  return (
    <div className="messaging-workspace">
      <div className="messaging-shell">
        <Segment className="messaging-sidebar">
          <div className="messaging-sidebar-header">
            <div className="messaging-sidebar-title">
              <Icon name="comments" />
              Messages
            </div>
            <Popup
              content="Reload saved conversations and joined rooms from the daemon."
              trigger={
                <Button
                  aria-label="Refresh messages workspace"
                  icon="refresh"
                  onClick={() => hydrate()}
                  size="mini"
                  title="Refresh messages workspace"
                />
              }
            />
          </div>

          <div className="messaging-sidebar-section">
            <div className="messaging-sidebar-section-title">Direct Message</div>
            <div className="messaging-start-row">
              <Input
                aria-label="Chat username"
                fluid
                onChange={(event) => setChatTarget(event.target.value)}
                onKeyUp={(event) => {
                  if (event.key === 'Enter' && chatTarget.trim()) {
                    openPanel('chat', chatTarget);
                    setChatTarget('');
                  }
                }}
                placeholder="username"
                size="small"
                value={chatTarget}
              />
              <Popup
                content="Open a direct-message panel for this user."
                trigger={
                  <Button
                    aria-label="Open direct-message panel"
                    disabled={!chatTarget.trim()}
                    icon="comment"
                    onClick={() => {
                      openPanel('chat', chatTarget);
                      setChatTarget('');
                    }}
                    size="small"
                    title="Open direct-message panel"
                  />
                }
              />
            </div>
          </div>

          <div className="messaging-sidebar-section">
            <div className="messaging-sidebar-section-title">
              Saved Chats
              <Label size="mini">{conversations.length}</Label>
            </div>
            <div className="messaging-list">
              {conversations.map((conversation) => (
                <div
                  className="messaging-list-action-row"
                  key={conversation.username}
                >
                  <Popup
                    content="Open this conversation as a workspace panel."
                    trigger={
                      <Button
                        basic
                        className="messaging-list-button"
                        compact
                        onClick={() => openPanel('chat', conversation.username)}
                        size="small"
                      >
                        <Icon name="comment alternate" />
                        {conversation.username}
                        {bridgedPodNames.has(
                          normalizeConversationName(conversation.username),
                        ) && (
                          <Label
                            size="mini"
                            title="Pod direct channel is folded into this saved direct message."
                          >
                            pod
                          </Label>
                        )}
                        {conversation.hasUnAcknowledgedMessages && (
                          <Label
                            color="red"
                            size="mini"
                          >
                            {conversation.unAcknowledgedMessageCount}
                          </Label>
                        )}
                      </Button>
                    }
                  />
                  <Popup
                    content="Permanently delete this saved message thread."
                    trigger={
                      <Button
                        aria-label={`Delete message thread with ${conversation.username}`}
                        icon="trash alternate"
                        negative
                        onClick={() => deleteConversation(conversation.username)}
                        size="small"
                        title={`Delete message thread with ${conversation.username}`}
                      />
                    }
                  />
                </div>
              ))}
            </div>
          </div>

          <div className="messaging-sidebar-section">
            <div className="messaging-sidebar-section-title">Join Room</div>
            <div className="messaging-start-row">
              <Dropdown
                aria-label="Search rooms"
                clearable
                fluid
                loading={roomSearchLoading}
                onChange={(_, { value }) => joinRoom(value)}
                onOpen={fetchAvailableRooms}
                options={roomOptions}
                placeholder="Search rooms"
                search
                selection
                size="small"
              />
              <RoomCreateModal onCreateRoom={(roomName) => joinRoom(roomName)} />
            </div>
          </div>

          <div className="messaging-sidebar-section">
            <div className="messaging-sidebar-section-title">
              Joined Rooms
              <Label size="mini">{joinedRooms.length}</Label>
            </div>
            <div className="messaging-list">
              {joinedRooms.map((roomName) => (
                <div
                  className="messaging-list-action-row"
                  key={roomName}
                >
                  <Popup
                    content="Open this room as a workspace panel."
                    trigger={
                      <Button
                        basic
                        className="messaging-list-button"
                        compact
                        onClick={() => openPanel('room', roomName)}
                        size="small"
                      >
                        <Icon name="comments" />
                        #{roomName}
                      </Button>
                    }
                  />
                  <Popup
                    content="Leave this room and remove it from joined rooms."
                    trigger={
                      <Button
                        aria-label={`Leave room ${roomName}`}
                        icon="sign-out"
                        negative
                        onClick={() => leaveRoom(roomName)}
                        size="small"
                        title={`Leave room ${roomName}`}
                      />
                    }
                  />
                </div>
              ))}
            </div>
          </div>

          <div className="messaging-sidebar-section">
            <div className="messaging-sidebar-section-title">
              Pod Channels
              <Label size="mini">{visiblePodChannels.length}</Label>
            </div>
            <div className="messaging-list">
              {visiblePodChannels.map((channel) => (
                <div
                  className="messaging-list-action-row"
                  key={channel.target}
                >
                  <Popup
                    content="Open this pod channel in the unified message workspace."
                    trigger={
                      <Button
                        basic
                        className="messaging-list-button"
                        compact
                        onClick={() =>
                          openPanel('pod', channel.target, {
                            label: channelLabel(channel),
                          })
                        }
                        size="small"
                      >
                        <Icon name="comments outline" />
                        {channelLabel(channel)}
                      </Button>
                    }
                  />
                  <Popup
                    content="Leave this pod and remove its channels from Messages."
                    trigger={
                      <Button
                        aria-label={`Leave pod ${channel.podName}`}
                        icon="sign-out"
                        negative
                        onClick={() => leavePod(channel)}
                        size="small"
                        title={`Leave pod ${channel.podName}`}
                      />
                    }
                  />
                </div>
              ))}
            </div>
          </div>
        </Segment>

        <div className="messaging-main">
          <Segment className="messaging-toolbar">
            <div className="messaging-toolbar-title">
              <Icon name="window restore outline" />
              Workspace
              <Label size="small">{openPanels.length} open</Label>
            </div>
            <Popup
              content="Collapse every open message panel into the dock."
              trigger={
                <Button
                  aria-label="Collapse all message panels"
                  disabled={openPanels.length === 0}
                  icon="window minimize outline"
                  onClick={() =>
                    setPanels((previous) =>
                      previous.map((panel) => ({ ...panel, collapsed: true })),
                    )
                  }
                  size="small"
                  title="Collapse all message panels"
                />
              }
            />
          </Segment>

          {openPanels.length === 0 ? (
            <PlaceholderSegment
              caption="Open a saved chat, joined room, or pod channel to start a workspace panel"
              className="messaging-empty"
              icon="comments"
            />
          ) : (
            <div className="messaging-window-grid">
              {openPanels.map((panel) => {
                const panelPodChannel =
                  panel.type === 'pod'
                    ? podChannels.find(
                      (channel) => channel.target === panel.target,
                    ) || {
                      ...decodePodTarget(panel.target),
                      podName: panel.label,
                    }
                    : null;

                return (
                  <Card
                    className="messaging-window"
                    key={panel.id}
                  >
                    <Card.Content className="messaging-window-title">
                      <div className="messaging-window-heading">
                        <Icon
                          name={
                            panel.type === 'room'
                              ? 'comments'
                              : panel.type === 'pod'
                                ? 'comments outline'
                                : 'comment'
                          }
                        />
                        <span>
                          {panel.type === 'chat' ? (
                            <UserCard username={panel.target}>{panel.target}</UserCard>
                          ) : (
                            panelLabel(panel)
                          )}
                        </span>
                      </div>
                      <div className="messaging-window-actions">
                        {panel.type === 'chat' && (
                          <>
                            <Popup
                              content="Open this user's profile."
                              trigger={
                                <Button
                                  aria-label={`Open ${panel.target} profile`}
                                  icon="user"
                                  onClick={() =>
                                    navigate('/users', { state: { user: panel.target } })
                                  }
                                  size="mini"
                                  title={`Open ${panel.target} profile`}
                                />
                              }
                            />
                            <Popup
                              content="Permanently delete this saved message thread."
                              trigger={
                                <Button
                                  aria-label={`Delete message thread with ${panel.target}`}
                                  icon="trash alternate"
                                  negative
                                  onClick={() => deleteConversation(panel.target)}
                                  size="mini"
                                  title={`Delete message thread with ${panel.target}`}
                                />
                              }
                            />
                          </>
                        )}
                        {panel.type === 'room' && (
                          <Popup
                            content="Leave this room and remove it from joined rooms."
                            trigger={
                              <Button
                                aria-label={`Leave room ${panel.target}`}
                                icon="sign-out"
                                negative
                                onClick={() => leaveRoom(panel.target)}
                                size="mini"
                                title={`Leave room ${panel.target}`}
                              />
                            }
                          />
                        )}
                        {panel.type === 'pod' && panelPodChannel && (
                          <Popup
                            content="Leave this pod and remove its channels from Messages."
                            trigger={
                              <Button
                                aria-label={`Leave pod ${panelPodChannel.podName}`}
                                icon="sign-out"
                                negative
                                onClick={() => leavePod(panelPodChannel)}
                                size="mini"
                                title={`Leave pod ${panelPodChannel.podName}`}
                              />
                            }
                          />
                        )}
                        <Popup
                          content="Collapse this panel into the message dock."
                          trigger={
                            <Button
                              aria-label={`Collapse ${panelLabel(panel)}`}
                              icon="window minimize outline"
                              onClick={() => setPanelCollapsed(panel.id, true)}
                              size="mini"
                              title={`Collapse ${panelLabel(panel)}`}
                            />
                          }
                        />
                        <Popup
                          content="Close this panel without deleting the thread or leaving anything."
                          trigger={
                            <Button
                              aria-label={`Close ${panelLabel(panel)}`}
                              icon="close"
                              onClick={() => closePanel(panel.id)}
                              size="mini"
                              title={`Close ${panelLabel(panel)}`}
                            />
                          }
                        />
                      </div>
                    </Card.Content>
                    <Card.Content className="messaging-window-body">
                      {panel.type === 'chat' ? (
                        <ChatSession
                          active
                          onDelete={() => closePanel(panel.id)}
                          user={state?.user}
                          username={panel.target}
                        />
                      ) : panel.type === 'pod' ? (
                        <PodChannelSession
                          channel={panelPodChannel}
                          state={state}
                        />
                      ) : (
                        <RoomSession
                          active
                          onBrowseShares={(username) =>
                            navigate('/browse', { state: { user: username } })
                          }
                          onLeaveRoom={leaveRoom}
                          onUserProfile={(username) =>
                            navigate('/users', { state: { user: username } })
                          }
                          roomName={panel.target}
                        />
                      )}
                    </Card.Content>
                  </Card>
                );
              })}
            </div>
          )}

          {collapsedPanels.length > 0 && (
            <div className="messaging-dock">
              {collapsedPanels.map((panel) => (
                <Popup
                  content="Restore this message panel."
                  key={panel.id}
                  trigger={
                    <Button
                      basic
                      compact
                      onClick={() => setPanelCollapsed(panel.id, false)}
                      size="small"
                    >
                      <Icon name={panel.type === 'room' ? 'comments' : 'comment'} />
                      {panelLabel(panel)}
                    </Button>
                  }
                />
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default Messaging;
