import './Messaging.css';
import * as chat from '../../lib/chat';
import * as rooms from '../../lib/rooms';
import { getLocalStorageItem, setLocalStorageItem } from '../../lib/storage';
import ChatSession from '../Chat/ChatSession';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import RoomCreateModal from '../Rooms/RoomCreateModal';
import RoomSession from '../Rooms/RoomSession';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Card,
  Dropdown,
  Icon,
  Input,
  Label,
  Popup,
  Segment,
} from 'semantic-ui-react';

const STORAGE_KEY = 'slskd-messaging-workspace';

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

const panelLabel = (panel) =>
  panel.type === 'room' ? `#${panel.target}` : panel.target;

const Messaging = ({ initialKind = 'mixed', state }) => {
  const navigate = useNavigate();
  const [panels, setPanels] = useState(() => loadPanels());
  const [chatTarget, setChatTarget] = useState('');
  const [conversations, setConversations] = useState([]);
  const [joinedRooms, setJoinedRooms] = useState([]);
  const [availableRooms, setAvailableRooms] = useState([]);
  const [roomSearchLoading, setRoomSearchLoading] = useState(false);

  const openPanel = useCallback((type, target) => {
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
          panel.id === existing.id ? { ...panel, collapsed: false } : panel,
        );
      }

      return [...previous, makePanel(type, trimmed)];
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
    const [serverConversations, serverJoinedRooms] = await Promise.all([
      chat.getAll(),
      rooms.getJoined(),
    ]);

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
  }, []);

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
    if (panels.length > 0) {
      return;
    }

    if (initialKind === 'chat' && conversations[0]?.username) {
      openPanel('chat', conversations[0].username);
    }

    if (initialKind === 'room' && joinedRooms[0]) {
      openPanel('room', joinedRooms[0]);
    }
  }, [conversations, initialKind, joinedRooms, openPanel, panels.length]);

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
                <Popup
                  content="Open this conversation as a workspace panel."
                  key={conversation.username}
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
                <Popup
                  content="Open this room as a workspace panel."
                  key={roomName}
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
              caption="Open a saved chat or joined room to start a workspace panel"
              className="messaging-empty"
              icon="comments"
            />
          ) : (
            <div className="messaging-window-grid">
              {openPanels.map((panel) => (
                <Card
                  className="messaging-window"
                  key={panel.id}
                >
                  <Card.Content className="messaging-window-title">
                    <div className="messaging-window-heading">
                      <Icon name={panel.type === 'room' ? 'comments' : 'comment'} />
                      <span>{panelLabel(panel)}</span>
                    </div>
                    <div className="messaging-window-actions">
                      {panel.type === 'chat' && (
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
                        content="Close this panel. This does not delete conversations or leave rooms."
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
              ))}
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
