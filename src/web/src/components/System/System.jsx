import './System.css';
import { Switch } from '../Shared';
import Data from './Data';
import Events from './Events';
import Files from './Files';
import Info from './Info';
import LibraryHealth from './LibraryHealth';
import Logs from './Logs';
import Mesh from './Mesh';
import Network from './Network';
import Options from './Options';
import Security from './Security';
import Shares from './Shares';
import React from 'react';
import { Redirect, useHistory, useRouteMatch } from 'react-router-dom';
import { Icon, Menu, Segment, Tab } from 'semantic-ui-react';

const System = ({ options = {}, state = {}, theme }) => {
  const {
    params: { tab },
    ...route
  } = useRouteMatch();
  const history = useHistory();

  const panes = [
    {
      menuItem: (
        <Menu.Item key="info">
          <Switch
            pending={
              ((state?.pendingRestart ?? false) ||
                (state?.pendingReconnect ?? false)) && (
                <Icon
                  color="yellow"
                  name="exclamation circle"
                />
              )
            }
          >
            <Icon name="info circle" />
          </Switch>
          Info
        </Menu.Item>
      ),
      render: () => (
        <Tab.Pane>
          <Info
            state={state}
            theme={theme}
          />
        </Tab.Pane>
      ),
      route: 'info',
    },
    {
      menuItem: (
        <Menu.Item key="network">
          <Icon
            color="blue"
            name="sitemap"
          />
          Network
        </Menu.Item>
      ),
      render: () => (
        <Tab.Pane>
          <Network />
        </Tab.Pane>
      ),
      route: 'network',
    },
    {
      menuItem: {
        content: 'Mesh',
        icon: 'share alternate',
        key: 'mesh',
      },
      render: () => (
        <Tab.Pane>
          <Mesh />
        </Tab.Pane>
      ),
      route: 'mesh',
    },
    {
      menuItem: {
        content: 'Security',
        icon: 'shield alternate',
        key: 'security',
      },
      render: () => (
        <Tab.Pane>
          <Security />
        </Tab.Pane>
      ),
      route: 'security',
    },
    {
      menuItem: {
        content: 'Options',
        icon: 'options',
        key: 'options',
      },
      render: () => (
        <Tab.Pane className="full-height">
          <Options
            options={options}
            theme={theme}
          />
        </Tab.Pane>
      ),
      route: 'options',
    },
    {
      menuItem: (
        <Menu.Item key="shares">
          <Switch
            scanPending={
              (state?.shares?.scanPending ?? false) && (
                <Icon
                  color="yellow"
                  name="exclamation circle"
                />
              )
            }
          >
            <Icon name="share external" />
          </Switch>
          Shares
        </Menu.Item>
      ),
      render: () => (
        <Tab.Pane>
          <Shares
            state={state.shares}
            theme={theme}
          />
        </Tab.Pane>
      ),
      route: 'shares',
    },
    {
      menuItem: {
        content: 'Library Health',
        icon: 'heartbeat',
        key: 'library-health',
      },
      render: () => (
        <Tab.Pane className="full-height">
          <LibraryHealth />
        </Tab.Pane>
      ),
      route: 'library-health',
    },
    {
      menuItem: {
        content: 'Files',
        icon: 'folder open',
        key: 'files',
      },
      render: () => (
        <Tab.Pane className="full-height">
          <Files
            options={options}
            theme={theme}
          />
        </Tab.Pane>
      ),
      route: 'files',
    },
    {
      menuItem: {
        content: 'Data',
        icon: 'database',
        key: 'data',
      },
      render: () => (
        <Tab.Pane className="full-height">
          <Data theme={theme} />
        </Tab.Pane>
      ),
      route: 'data',
    },
    {
      menuItem: {
        content: 'Events',
        icon: 'calendar check',
        key: 'events',
      },
      render: () => (
        <Tab.Pane className="full-height">
          <Events />
        </Tab.Pane>
      ),
      route: 'events',
    },
    {
      menuItem: {
        content: 'Logs',
        icon: 'file outline',
        key: 'logs',
      },
      render: () => (
        <Tab.Pane>
          <Logs />
        </Tab.Pane>
      ),
      route: 'logs',
    },
  ];

  const activeIndex = panes.findIndex((pane) => pane.route === tab);

  const onTabChange = (_event, { activeIndex: newActiveIndex }) => {
    history.push(panes[newActiveIndex].route);
  };

  if (tab === undefined) {
    return <Redirect to={`${route.url}/${panes[0].route}`} />;
  }

  return (
    <div className="system">
      <Segment raised>
        <Tab
          activeIndex={activeIndex > -1 ? activeIndex : 0}
          onTabChange={onTabChange}
          panes={panes}
        />
      </Segment>
    </div>
  );
};

export default System;
