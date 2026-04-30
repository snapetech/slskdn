import './Files.css';
import Explorer from './Explorer';
import React, { useState } from 'react';
import { Tab } from 'semantic-ui-react';

const Files = ({ options } = {}) => {
  const { remoteFileManagement } = options;
  const [activeIndex, setActiveIndex] = useState(0);

  const panes = [
    {
      menuItem: 'Downloads',
      render: () => (
        <Tab.Pane>
          <Explorer
            remoteFileManagement={remoteFileManagement}
            root="downloads"
          />
        </Tab.Pane>
      ),
      route: 'downloads',
    },
    {
      menuItem: 'Incomplete',
      render: () => (
        <Tab.Pane>
          <Explorer
            remoteFileManagement={remoteFileManagement}
            root="incomplete"
          />
        </Tab.Pane>
      ),
      route: 'incomplete',
    },
  ];

  return (
    <div>
      <Tab
        activeIndex={activeIndex}
        onTabChange={(_event, { activeIndex: nextIndex }) =>
          setActiveIndex(nextIndex)
        }
        panes={panes}
        renderActiveOnly={false}
      />
    </div>
  );
};

export default Files;
