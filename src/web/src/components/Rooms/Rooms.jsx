import './Rooms.css';
import { getLocalStorageItem, setLocalStorageItem } from '../../lib/storage';
import * as rooms from '../../lib/rooms';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import RoomCreateModal from './RoomCreateModal';
import RoomSession from './RoomSession';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Dropdown,
  Icon,
  Menu,
  Popup,
  Segment,
  Tab,
} from 'semantic-ui-react';

let tabCounter = 0;

// Load tabs from localStorage
const loadTabsFromStorage = () => {
  try {
    const saved = getLocalStorageItem('slskd-room-tabs');

    if (saved) {
      const parsed = JSON.parse(saved);
      // Restore tabCounter to avoid key collisions
      tabCounter = parsed.tabCounter || 0;
      return parsed.tabs || [];
    }
  } catch {
    // ignore
  }

  return [];
};

// Save tabs to localStorage
const saveTabsToStorage = (tabsToSave) => {
  setLocalStorageItem(
    'slskd-room-tabs',
    JSON.stringify({ tabCounter, tabs: tabsToSave }),
  );
};

const Rooms = () => {
  const navigate = useNavigate();
  const [tabs, setTabs] = useState(() => loadTabsFromStorage());
  const [activeIndex, setActiveIndex] = useState(0);
  const [availableRooms, setAvailableRooms] = useState([]);
  const [joinedRooms, setJoinedRooms] = useState([]);
  const [roomSearchLoading, setRoomSearchLoading] = useState(false);
  const closeTabRef = useRef(null);

  const closeTab = useCallback((tabKey) => {
    setTabs((previous) => {
      const newTabs = previous.filter((t) => t.key !== tabKey);
      setActiveIndex((currentIndex) =>
        currentIndex >= newTabs.length
          ? Math.max(0, newTabs.length - 1)
          : currentIndex,
      );
      return newTabs;
    });
  }, []);

  closeTabRef.current = closeTab;

  const createTab = useCallback((roomName = '') => {
    tabCounter += 1;
    const tabKey = `room-tab-${tabCounter}`;
    return {
      key: tabKey,
      label: roomName || 'New Room Tab',
      roomName,
    };
  }, []);

  // Create initial tab on mount
  useEffect(() => {
    if (tabs.length === 0) {
      setTabs([createTab()]);
    }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // Auto-create tab if all closed, and reset counter to keep numbers reasonable
  useEffect(() => {
    if (tabs.length === 0) {
      tabCounter = 0; // Reset counter when starting fresh
      setTabs([createTab()]);
    }
  }, [tabs.length, createTab]);

  // Save tabs to localStorage whenever they change
  useEffect(() => {
    if (tabs.length > 0) {
      saveTabsToStorage(tabs);
    }
  }, [tabs]);

  const openRoomTab = useCallback(
    (roomName) => {
      if (!roomName) return;

      const existingTabIndex = tabs.findIndex((t) => t.roomName === roomName);
      if (existingTabIndex === -1) {
        setTabs((previous) => {
          const newTabs = [...previous, createTab(roomName)];
          setActiveIndex(newTabs.length - 1);
          return newTabs;
        });
      } else {
        setActiveIndex(existingTabIndex);
      }
    },
    [createTab, tabs],
  );

  const hydrateJoinedRooms = useCallback(async () => {
    try {
      const joined = await rooms.getJoined();
      const normalized = (joined || []).filter(Boolean).sort();
      setJoinedRooms(normalized);
      if (normalized.length > 0) {
        setTabs((previous) => {
          const existingRooms = new Set(
            previous.map((tab) => tab.roomName).filter(Boolean),
          );
          const restoredTabs = normalized
            .filter((roomName) => !existingRooms.has(roomName))
            .map((roomName) => createTab(roomName));

          return restoredTabs.length > 0
            ? [...previous.filter((tab) => tab.roomName), ...restoredTabs]
            : previous;
        });
      }
    } catch (error) {
      console.error('Failed to fetch joined rooms:', error);
    }
  }, [createTab]);

  useEffect(() => {
    hydrateJoinedRooms();
    const interval = window.setInterval(hydrateJoinedRooms, 10_000);
    return () => window.clearInterval(interval);
  }, [hydrateJoinedRooms]);

  const fetchAvailableRooms = async () => {
    setRoomSearchLoading(true);
    try {
      const available = await rooms.getAvailable();
      setAvailableRooms(available || []);
    } catch {
      setAvailableRooms([]);
    } finally {
      setRoomSearchLoading(false);
    }
  };

  const joinRoom = async (roomName) => {
    try {
      await rooms.join({ roomName });

      // Refresh joined rooms
      const joined = await rooms.getJoined();
      setJoinedRooms(joined || []);
      openRoomTab(roomName);
    } catch (error) {
      console.error('Failed to join room:', error);
    }
  };

  const leaveRoom = async (roomName) => {
    try {
      await rooms.leave({ roomName });

      // Refresh joined rooms
      const joined = await rooms.getJoined();
      setJoinedRooms(joined || []);

      // Close the tab for this room
      const tabToClose = tabs.find((t) => t.roomName === roomName);
      if (tabToClose) {
        closeTabRef.current?.(tabToClose.key);
      }
    } catch (error) {
      console.error('Failed to leave room:', error);
    }
  };

  const createRoom = async (roomName, isPrivate) => {
    // For now, private room creation isn't directly supported by Soulseek protocol
    // We just attempt to join the room, which may create it if it doesn't exist
    await joinRoom(roomName);
  };

  const handleAddTab = () => {
    setTabs((previous) => {
      const newTabs = [...previous, createTab()];
      setActiveIndex(newTabs.length - 1);
      return newTabs;
    });
  };

  const handleUserProfile = useCallback(
    (username) => {
      navigate('/users', { state: { user: username } });
    },
    [navigate],
  );

  const handleBrowseShares = useCallback(
    (username) => {
      navigate('/browse', { state: { user: username } });
    },
    [navigate],
  );

  const roomOptions = availableRooms.map((r) => ({
    description: r.isPrivate ? 'Private' : '',
    key: r.name,
    text: `${r.name} (${r.userCount} users)`,
    value: r.name,
  }));

  const panes = tabs.map((tab, index) => ({
    menuItem: (
      <Menu.Item key={tab.key}>
        <Icon name={tab.roomName ? 'comments' : 'search'} />
        {tab.label}
        {tabs.length > 1 && (
          <Icon
            name="close"
            onClick={(event) => {
              event.stopPropagation();
              closeTabRef.current?.(tab.key);
            }}
            style={{ marginLeft: '8px', opacity: 0.7 }}
          />
        )}
      </Menu.Item>
    ),
    render: () => (
      <Tab.Pane
        attached={false}
        key={tab.key}
        style={{ border: 'none', boxShadow: 'none' }}
      >
        <RoomSession
          active={index === activeIndex}
          key={tab.key}
          onBrowseShares={handleBrowseShares}
          onLeaveRoom={leaveRoom}
          onUserProfile={handleUserProfile}
          roomName={tab.roomName}
        />
      </Tab.Pane>
    ),
  }));

  return (
    <div className="rooms">
      <Segment
        className="rooms-segment"
        raised
      >
        <div className="rooms-segment-icon">
          <Icon
            name="comments"
            size="big"
          />
        </div>
        <div
          style={{
            display: 'flex',
            flex: 1,
            flexDirection: 'column',
            gap: '8px',
          }}
        >
          <div style={{ alignItems: 'center', display: 'flex', gap: '8px' }}>
            <Dropdown
              className="rooms-input"
              clearable
              fluid
              loading={roomSearchLoading}
              onChange={(_, { value }) => {
                if (value) {
                  joinRoom(value);
                }
              }}
              onOpen={() => fetchAvailableRooms()}
              options={roomOptions}
              placeholder="Search existing rooms..."
              search
              selection
            />
            <RoomCreateModal onCreateRoom={createRoom} />
            <Popup
              content="Reload rooms joined by the daemon and reopen their tabs."
              trigger={
                <Button
                  icon="refresh"
                  onClick={hydrateJoinedRooms}
                />
              }
            />
          </div>
        </div>
      </Segment>
      {joinedRooms.length > 0 && (
        <Segment className="room-recovery-rail">
          {joinedRooms.map((roomName) => (
            <Popup
              content="Open this joined room."
              key={roomName}
              trigger={
                <Button
                  basic
                  compact
                  onClick={() => openRoomTab(roomName)}
                  size="small"
                >
                  <Icon name="comments outline" />
                  {roomName}
                </Button>
              }
            />
          ))}
        </Segment>
      )}
      <Tab
        activeIndex={activeIndex}
        menu={{
          attached: false,
          inverted: true,
          secondary: true,
          tabular: false,
        }}
        onTabChange={(event, { activeIndex: newIndex }) =>
          setActiveIndex(newIndex)
        }
        panes={[
          ...panes,
          {
            menuItem: (
              <Menu.Item
                key="add-tab"
                onClick={handleAddTab}
              >
                <Icon name="plus" />
              </Menu.Item>
            ),
            render: () => null,
          },
        ]}
        renderActiveOnly={false}
      />
    </div>
  );
};

export default Rooms;
