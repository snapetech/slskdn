import './Chat.css';
import * as chat from '../../lib/chat';
import ChatSession from './ChatSession';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { Icon, Input, Menu, Segment, Tab } from 'semantic-ui-react';

let tabCounter = 0;

// Load tabs from localStorage
const loadTabsFromStorage = () => {
  try {
    const saved = localStorage.getItem('slskd-chat-tabs');

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
      'slskd-chat-tabs',
      JSON.stringify({ tabCounter, tabs: tabsToSave }),
    );
  } catch {
    // ignore
  }
};

const Chat = ({ state }) => {
  const location = useLocation();
  const [tabs, setTabs] = useState(() => loadTabsFromStorage());
  const [activeIndex, setActiveIndex] = useState(0);
  const [usernameInput, setUsernameInput] = useState('');
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

  const updateTabLabel = useCallback((tabKey, newUsername) => {
    setTabs((previous) =>
      previous.map((t) =>
        t.key === tabKey
          ? { ...t, label: newUsername, username: newUsername }
          : t,
      ),
    );
  }, []);

  closeTabRef.current = closeTab;
  updateTabRef.current = updateTabLabel;

  const createTab = useCallback((username = '') => {
    tabCounter += 1;
    const tabKey = `chat-tab-${tabCounter}`;
    return {
      key: tabKey,
      label: username || 'New Chat',
      username,
    };
  }, []);

  // Create initial tab on mount if none exist
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

  // Handle navigation with username in state (quick chat from user profile)
  useEffect(() => {
    const username = location.state?.user;

    if (username) {
      const existingIndex = tabs.findIndex((t) => t.username === username);

      if (existingIndex === -1) {
        // Create new tab for this user
        setTabs((previous) => {
          const newTabs = [...previous, createTab(username)];
          setActiveIndex(newTabs.length - 1);
          return newTabs;
        });
      } else {
        setActiveIndex(existingIndex);
      }

      // Clear the state to prevent re-triggering
      window.history.replaceState({}, document.title);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [location.state]);

  const startConversation = useCallback(
    (username) => {
      if (!username || !username.trim()) return;

      const trimmedUsername = username.trim();
      const existingIndex = tabs.findIndex(
        (t) => t.username === trimmedUsername,
      );

      if (existingIndex === -1) {
        // Create new tab for this user
        setTabs((previous) => {
          const newTabs = [...previous, createTab(trimmedUsername)];
          setActiveIndex(newTabs.length - 1);
          return newTabs;
        });
      } else {
        // Switch to existing tab
        setActiveIndex(existingIndex);
      }
    },
    [createTab, tabs],
  );

  const handleAddTab = () => {
    setTabs((previous) => {
      const newTabs = [...previous, createTab()];
      setActiveIndex(newTabs.length - 1);
      return newTabs;
    });
  };

  const handleDeleteConversation = useCallback(
    async (username) => {
      // Remove the conversation from chat API
      try {
        await chat.remove({ username });
      } catch (error) {
        console.error('Failed to remove conversation:', error);
      }

      // Close the tab
      const tabToClose = tabs.find((t) => t.username === username);
      if (tabToClose) {
        closeTabRef.current?.(tabToClose.key);
      }
    },
    [tabs],
  );

  const panes = tabs.map((tab) => ({
    menuItem: (
      <Menu.Item key={tab.key}>
        <Icon name={tab.username ? 'comment' : 'search'} />
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
        <ChatSession
          key={tab.key}
          onDelete={handleDeleteConversation}
          user={state?.user}
          username={tab.username}
        />
      </Tab.Pane>
    ),
  }));

  return (
    <div className="chats">
      <Segment
        className="chat-segment"
        raised
      >
        <div className="chat-segment-icon">
          <Icon
            name="comment"
            size="big"
          />
        </div>
        <Input
          action={{
            icon: 'chat',
            onClick: () => {
              if (usernameInput?.trim()) {
                startConversation(usernameInput.trim());
                setUsernameInput('');
              }
            },
          }}
          className="chat-input"
          onChange={(event) => setUsernameInput(event.target.value)}
          onKeyUp={(event) => {
            if (event.key === 'Enter' && usernameInput?.trim()) {
              startConversation(usernameInput.trim());
              setUsernameInput('');
            }
          }}
          placeholder="Chat with user..."
          size="big"
          value={usernameInput}
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

export default Chat;
