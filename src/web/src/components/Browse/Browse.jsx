import BrowseSession from './BrowseSession';
import React, { useEffect, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { Icon, Tab } from 'semantic-ui-react';

const Browse = () => {
  const location = useLocation();
  const [panes, setPanes] = useState([
    {
      key: 'default',
      menuItem: { content: 'New Tab', icon: 'plus', key: 'default' },
      render: () => <BrowseSession />,
      username: '',
    },
  ]);
  const [activeIndex, setActiveIndex] = useState(0);

  const closeTab = (username) => {
    setPanes((previous) => {
      const newPanes = previous.filter((p) => p.username !== username);
      // If we closed the active tab, switch to the last one
      if (activeIndex >= newPanes.length) {
        setActiveIndex(Math.max(0, newPanes.length - 1));
      }

      return newPanes;
    });
  };

  useEffect(() => {
    const user = location.state?.user;
    if (user) {
      // Check if tab exists
      const existingIndex = panes.findIndex((p) => p.username === user);
      if (existingIndex === -1) {
        const newPane = {
          key: user,
          menuItem: {
            content: (
              <span>
                {user}
                <Icon
                  name="close"
                  onClick={(event) => {
                    event.stopPropagation();
                    closeTab(user);
                  }}
                  style={{ marginLeft: '10px', marginRight: '-5px' }}
                />
              </span>
            ),
            key: user,
          },
          render: () => <BrowseSession username={user} />,
          username: user,
        };
        setPanes((previous) => [...previous, newPane]);
        setActiveIndex(panes.length); // Activate new tab (it will be at end)
      } else {
        setActiveIndex(existingIndex);
      }
    }
  }, [location.state]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleTabChange = (event, { activeIndex: newIndex }) =>
    setActiveIndex(newIndex);

  return (
    <div className="search-container">
      <Tab
        activeIndex={activeIndex}
        menu={{
          attached: true,
          pointing: true,
          secondary: true,
          tabular: false,
        }}
        onTabChange={handleTabChange}
        panes={panes}
        renderActiveOnly={false}
      />
    </div>
  );
};

export default Browse;
