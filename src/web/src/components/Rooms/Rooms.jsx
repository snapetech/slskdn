import { activeRoomKey } from '../../config';
import * as rooms from '../../lib/rooms';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import RoomSession from './RoomSession';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useHistory } from 'react-router-dom';
import {
  Button,
  Card,
  Dimmer,
  Dropdown,
  Icon,
  Input,
  List,
  Loader,
  Menu,
  Portal,
  Ref,
  Segment,
  Tab,
} from 'semantic-ui-react';

let tabCounter = 0;

// Load tabs from localStorage
const loadTabsFromStorage = () => {
  try {
    const saved = localStorage.getItem('slskd-room-tabs');

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
  try {
    localStorage.setItem(
      'slskd-room-tabs',
      JSON.stringify({ tabCounter, tabs: tabsToSave }),
    );
  } catch {
    // ignore
  }
};

const Rooms = () => {
  const history = useHistory();
  const [tabs, setTabs] = useState(() => loadTabsFromStorage());
  const [activeIndex, setActiveIndex] = useState(0);
  const [availableRooms, setAvailableRooms] = useState([]);
  const [joinedRooms, setJoinedRooms] = useState([]);
  const [roomSearchLoading, setRoomSearchLoading] = useState(false);
  const closeTabRef = useRef(null);
  const updateTabRef = useRef(null);

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

  const updateTabLabel = useCallback((tabKey, newRoomName) => {
    setTabs((previous) =>
      previous.map((t) =>
        t.key === tabKey
          ? { ...t, label: newRoomName, roomName: newRoomName }
          : t,
      ),
    );
  }, []);

  closeTabRef.current = closeTab;
  updateTabRef.current = updateTabLabel;

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

  // Fetch joined rooms on mount
  useEffect(() => {
    const fetchJoinedRooms = async () => {
      try {
        const joined = await rooms.getJoined();
        setJoinedRooms(joined || []);
      } catch (error) {
        console.error('Failed to fetch joined rooms:', error);
      }
    };

    fetchJoinedRooms();
  }, []);

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

      // Check if we already have a tab for this room
      const existingTabIndex = tabs.findIndex((t) => t.roomName === roomName);

      if (existingTabIndex === -1) {
        // Create new tab for this room
        setTabs((previous) => {
          const newTabs = [...previous, createTab(roomName)];
          setActiveIndex(newTabs.length - 1);
          return newTabs;
        });
      } else {
        // Switch to existing tab
        setActiveIndex(existingTabIndex);
      }
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

  const handleAddTab = () => {
    setTabs((previous) => {
      const newTabs = [...previous, createTab()];
      setActiveIndex(newTabs.length - 1);
      return newTabs;
    });
  };

  const handleUserProfile = useCallback((username) => {
    history.push('/users', { user: username });
  }, [history]);

  const handleBrowseShares = useCallback((username) => {
    history.push('/browse', { user: username });
  }, [history]);

  const roomOptions = availableRooms.map((r) => ({
    description: r.isPrivate ? 'Private' : '',
    key: r.name,
    text: `${r.name} (${r.userCount} users)`,
    value: r.name,
  }));

  const panes = tabs.map((tab) => ({
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
          key={tab.key}
          roomName={tab.roomName}
          onLeaveRoom={leaveRoom}
          onUserProfile={handleUserProfile}
          onBrowseShares={handleBrowseShares}
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
          placeholder="Search rooms..."
          search
          selection
        />
      </Segment>
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
      />
    </div>
  );
};

export default Rooms;
