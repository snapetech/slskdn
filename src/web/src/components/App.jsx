import 'react-toastify/dist/ReactToastify.css';
import './App.css';
import { createApplicationHubConnection } from '../lib/hubFactory';
import * as relayAPI from '../lib/relay';
import { connect, disconnect } from '../lib/server';
import * as session from '../lib/session';
import { isPassthroughEnabled } from '../lib/token';
import AppContext from './AppContext';
import Browse from './Browse/Browse';
import Chat from './Chat/Chat';
import Collections from './Collections/Collections';
import Contacts from './Contacts/Contacts';
import LoginForm from './LoginForm';
import Pods from './Pods/Pods';
import Rooms from './Rooms/Rooms';
import Searches from './Search/Searches';
import {
  isStatusBarVisible,
  SlskdnStatusBar,
  toggleStatusBarVisibility,
} from './Shared';
import ErrorSegment from './Shared/ErrorSegment';
import Footer from './Shared/Footer';
import ShareGroups from './ShareGroups/ShareGroups';
import SharedWithMe from './Shares/SharedWithMe';
import System from './System/System';
import Transfers from './Transfers/Transfers';
import Users from './Users/Users';
import Wishlist from './Wishlist/Wishlist';
import React, { Component } from 'react';
import { Link, Redirect, Route, Switch } from 'react-router-dom';
import { ToastContainer } from 'react-toastify';
import {
  Button,
  Header,
  Icon,
  Loader,
  Menu,
  Modal,
  Segment,
  Sidebar,
} from 'semantic-ui-react';

const initialState = {
  applicationOptions: {},
  applicationState: {},
  error: false,
  initialized: false,
  login: {
    error: undefined,
    pending: false,
  },
  retriesExhausted: false,
  statusBarVisible: isStatusBarVisible(),
};

const ModeSpecificConnectButton = ({
  connectionWatchdog,
  controller = {},
  mode,
  pendingReconnect,
  server,
  user,
}) => {
  if (mode === 'Agent') {
    const isConnected = controller?.state === 'Connected';
    const isTransitioning = ['Connecting', 'Reconnecting'].includes(
      controller?.state,
    );

    return (
      <Menu.Item
        onClick={() =>
          isConnected ? relayAPI.disconnect() : relayAPI.connect()
        }
      >
        <Icon.Group className="menu-icon-group">
          <Icon
            color={
              controller?.state === 'Connected'
                ? 'green'
                : isTransitioning
                  ? 'yellow'
                  : 'grey'
            }
            name="plug"
          />
          {!isConnected && (
            <Icon
              className="menu-icon-no-shadow"
              color="red"
              corner="bottom right"
              name="close"
            />
          )}
        </Icon.Group>
        Controller {controller?.state}
      </Menu.Item>
    );
  } else {
    if (server?.isConnected) {
      return (
        <Menu.Item onClick={() => disconnect()}>
          <Icon.Group className="menu-icon-group">
            <Icon
              color={pendingReconnect ? 'yellow' : 'green'}
              name="plug"
            />
            {user?.privileges?.isPrivileged && (
              <Icon
                className="menu-icon-no-shadow"
                color="yellow"
                corner
                name="star"
              />
            )}
          </Icon.Group>
          Connected
        </Menu.Item>
      );
    }

    // the server is disconnected, and we need to give the user some information about what the client is doing
    // options are:
    // - nothing. the client was manually disconnected, kicked off by another login, etc., and we're not trying to connect
    // - actively trying to make a connection to the server
    // - still trying to connect, but waiting for the next connection attempt
    let icon = 'close';
    let color = 'red';

    if (connectionWatchdog?.isAttemptingConnection) {
      icon = 'clock';
      color = 'yellow';
    }

    if (server?.isConnecting || server?.IsLoggingIn) {
      icon = 'sync alternate loading';
      color = 'green';
    }

    return (
      <Menu.Item onClick={() => connect()}>
        <Icon.Group className="menu-icon-group">
          <Icon
            color="grey"
            name="plug"
          />
          <Icon
            className="menu-icon-no-shadow"
            color={color}
            corner="bottom right"
            name={icon}
          />
        </Icon.Group>
        Disconnected
      </Menu.Item>
    );
  }
};

class App extends Component {
  constructor(props) {
    super(props);

    this.state = initialState;
  }

  componentDidMount() {
    if (this.getSavedTheme() == null) {
      window
        .matchMedia('(prefers-color-scheme: dark)')
        .addEventListener(
          'change',
          (event) => event.matches && this.setState({ theme: 'dark' }),
        );
      window
        .matchMedia('(prefers-color-scheme: light)')
        .addEventListener(
          'change',
          (event) => event.matches && this.setState({ theme: 'light' }),
        );
    }

    // Listen for status bar toggle from the status bar's close button
    window.addEventListener('slskdn-status-toggle', this.handleStatusBarToggle);

    this.init();
  }

  componentWillUnmount() {
    window.removeEventListener(
      'slskdn-status-toggle',
      this.handleStatusBarToggle,
    );
  }

  handleStatusBarToggle = () => {
    this.setState({ statusBarVisible: isStatusBarVisible() });
  };

  init = async () => {
    this.setState({ initialized: false }, async () => {
      try {
        const securityEnabled = await session.getSecurityEnabled();

        if (!securityEnabled) {
          console.debug('application security is not enabled, per api call');
          session.enablePassthrough();
        }

        if (await session.check()) {
          const appHub = createApplicationHubConnection();

          appHub.on('state', (state) => {
            this.setState({ applicationState: state });
          });

          appHub.on('options', (options) => {
            this.setState({ applicationOptions: options });
          });

          appHub.onreconnecting(() =>
            this.setState({ error: true, retriesExhausted: false }),
          );
          appHub.onclose(() =>
            this.setState({ error: true, retriesExhausted: true }),
          );
          appHub.onreconnected(() =>
            this.setState({ error: false, retriesExhausted: false }),
          );

          await appHub.start();
        }

        const savedTheme = this.getSavedTheme();
        if (savedTheme != null) {
          this.setState({ theme: savedTheme });
        }

        this.setState({
          error: false,
        });
      } catch (error) {
        console.error(error);
        this.setState({ error: true, retriesExhausted: true });
      } finally {
        this.setState({ initialized: true });
      }
    });
  };

  getSavedTheme = () => {
    return localStorage.getItem('slskd-theme');
  };

  toggleTheme = () => {
    this.setState((state) => {
      const newTheme = state.theme === 'dark' ? 'light' : 'dark';
      localStorage.setItem('slskd-theme', newTheme);
      return { theme: newTheme };
    });
  };

  toggleStatusBar = () => {
    const newVisible = toggleStatusBarVisibility();
    this.setState({ statusBarVisible: newVisible });
  };

  handleLogin = (username, password, rememberMe) => {
    this.setState(
      (previousState) => ({
        login: { ...previousState.login, error: undefined, pending: true },
      }),
      async () => {
        try {
          await session.login({ password, rememberMe, username });
          this.setState(
            (previousState) => ({
              login: { ...previousState.login, error: false, pending: false },
            }),
            () => this.init(),
          );
        } catch (error) {
          this.setState((previousState) => ({
            login: { ...previousState.login, error, pending: false },
          }));
        }
      },
    );
  };

  logout = () => {
    session.logout();
    this.setState({ login: { ...initialState.login } });
  };

  withTokenCheck = (component) => {
    session.check(); // async, runs in the background
    return component; // Return component directly, not spread object
  };

  // eslint-disable-next-line complexity
  render() {
    const {
      applicationOptions = {},
      applicationState = {},
      error,
      initialized,
      login,
      retriesExhausted,
      statusBarVisible,
      theme = this.getSavedTheme() ||
        (window.matchMedia('(prefers-color-scheme: dark)').matches
          ? 'dark'
          : 'light'),
    } = this.state;
    const {
      connectionWatchdog = {},
      pendingReconnect,
      pendingRestart,
      relay = {},
      server,
      shares = {},
      user,
      version = {},
    } = applicationState;
    const { current, isUpdateAvailable, latest } = version;
    const { scanPending: pendingShareRescan } = shares;

    const { controller, mode } = relay;

    if (!initialized) {
      return (
        <Loader
          active
          size="big"
        />
      );
    }

    if (error) {
      return (
        <ErrorSegment
          caption={
            <>
              <span>Lost connection to slskd</span>
              <br />
              <span>
                {retriesExhausted ? 'Refresh to reconnect' : 'Retrying...'}
              </span>
            </>
          }
          icon="attention"
          suppressPrefix
        />
      );
    }

    if (!session.isLoggedIn() && !isPassthroughEnabled()) {
      return (
        <LoginForm
          error={login.error}
          initialized={login.initialized}
          loading={login.pending}
          onLoginAttempt={this.handleLogin}
        />
      );
    }

    const isAgent = mode === 'Agent';

    if (theme === 'dark') {
      document.documentElement.classList.add(theme);
    } else {
      document.documentElement.classList.remove('dark');
    }

    return (
      <>
        <Sidebar.Pushable
          as={Segment}
          className="app"
        >
          <Sidebar
            animation="overlay"
            as={Menu}
            className="navigation"
            direction="top"
            horizontal="true"
            icon="labeled"
            inverted
            visible
            width="thin"
          >
            {version.isCanary && (
              <Menu.Item>
                <Icon
                  color="yellow"
                  name="flask"
                />
                Canary
              </Menu.Item>
            )}
            {isAgent ? (
              <Menu.Item>
                <Icon name="detective" />
                Agent Mode
              </Menu.Item>
            ) : (
              <>
                <Link to="/searches">
                  <Menu.Item data-testid="nav-search">
                    <Icon name="search" />
                    Search
                  </Menu.Item>
                </Link>
                <Link to="/wishlist">
                  <Menu.Item data-testid="nav-wishlist">
                    <Icon name="star" />
                    Wishlist
                  </Menu.Item>
                </Link>
                <Link to="/downloads">
                  <Menu.Item data-testid="nav-downloads">
                    <Icon name="download" />
                    Downloads
                  </Menu.Item>
                </Link>
                <Link to="/uploads">
                  <Menu.Item data-testid="nav-uploads">
                    <Icon name="upload" />
                    Uploads
                  </Menu.Item>
                </Link>
                <Link to="/rooms">
                  <Menu.Item data-testid="nav-rooms">
                    <Icon name="comments" />
                    Rooms
                  </Menu.Item>
                </Link>
                <Link to="/chat">
                  <Menu.Item data-testid="nav-chat">
                    <Icon name="comment" />
                    Chat
                  </Menu.Item>
                </Link>
                <Link to="/users">
                  <Menu.Item data-testid="nav-users">
                    <Icon name="users" />
                    Users
                  </Menu.Item>
                </Link>
                <Link to="/contacts">
                  <Menu.Item data-testid="nav-contacts">
                    <Icon name="address book" />
                    Contacts
                  </Menu.Item>
                </Link>
                <Link to="/collections">
                  <Menu.Item data-testid="nav-collections">
                    <Icon name="list" />
                    Collections
                  </Menu.Item>
                </Link>
                <Link to="/sharegroups">
                  <Menu.Item data-testid="nav-groups">
                    <Icon name="users" />
                    Share Groups
                  </Menu.Item>
                </Link>
                <Link to="/shared">
                  <Menu.Item data-testid="nav-shared-with-me">
                    <Icon name="share" />
                    Shared with Me
                  </Menu.Item>
                </Link>
                <Link to="/browse">
                  <Menu.Item data-testid="nav-browse">
                    <Icon name="folder open" />
                    Browse
                  </Menu.Item>
                </Link>
              </>
            )}
            <Menu
              className="right"
              inverted
            >
              <Menu.Item
                className={`slskdn-toggle ${statusBarVisible ? 'active' : ''}`}
                onClick={() => this.toggleStatusBar()}
                title={
                  statusBarVisible
                    ? 'Hide slskdn status bar'
                    : 'Show slskdn status bar'
                }
              >
                <Icon
                  color={statusBarVisible ? 'green' : 'grey'}
                  name={statusBarVisible ? 'toggle on' : 'toggle off'}
                  size="small"
                />
              </Menu.Item>
              <Menu.Item onClick={() => this.toggleTheme()}>
                <Icon name="theme" />
                Theme
              </Menu.Item>
              <ModeSpecificConnectButton
                connectionWatchdog={connectionWatchdog}
                controller={controller}
                mode={mode}
                pendingReconnect={pendingReconnect}
                server={server}
                user={user}
              />
              {(pendingReconnect || pendingRestart || pendingShareRescan) && (
                <Menu.Item position="right">
                  <Icon.Group className="menu-icon-group">
                    <Link to="/system/info">
                      <Icon
                        color="yellow"
                        name="exclamation circle"
                      />
                    </Link>
                  </Icon.Group>
                  Pending Action
                </Menu.Item>
              )}
              {isUpdateAvailable && (
                <Modal
                  centered
                  closeIcon
                  size="mini"
                  trigger={
                    <Menu.Item position="right">
                      <Icon.Group className="menu-icon-group">
                        <Icon
                          color="yellow"
                          name="bullhorn"
                        />
                      </Icon.Group>
                      New Version!
                    </Menu.Item>
                  }
                >
                  <Modal.Header>New Version!</Modal.Header>
                  <Modal.Content>
                    <p>
                      You are currently running version{' '}
                      <strong>{current}</strong>
                      while version <strong>{latest}</strong> is available.
                    </p>
                  </Modal.Content>
                  <Modal.Actions>
                    <Button
                      fluid
                      href="https://github.com/slskd/slskd/releases"
                      primary
                      style={{ marginLeft: 0 }}
                    >
                      See Release Notes
                    </Button>
                  </Modal.Actions>
                </Modal>
              )}
              <Link to="/system">
                <Menu.Item data-testid="nav-system">
                  <Icon name="cogs" />
                  System
                </Menu.Item>
              </Link>
              {session.isLoggedIn() && (
                <Modal
                  actions={[
                    'Cancel',
                    {
                      content: 'Log Out',
                      key: 'done',
                      negative: true,
                      onClick: this.logout,
                    },
                  ]}
                  centered
                  content="Are you sure you want to log out?"
                  header={
                    <Header
                      content="Confirm Log Out"
                      icon="sign-out"
                    />
                  }
                  size="mini"
                  trigger={
                    <Menu.Item data-testid="logout">
                      <Icon name="sign-out" />
                      Log Out
                    </Menu.Item>
                  }
                />
              )}
            </Menu>
          </Sidebar>
          <Sidebar.Pusher className="app-content">
            {session.isLoggedIn() || isPassthroughEnabled() ? (
              <SlskdnStatusBar />
            ) : null}
            <AppContext.Provider
              // Note: Context value object recreated on each render (class component limitation)
              // Deferred: Optimize with useMemo when converting to functional component
              // See memory-bank/triage-todo-fixme.md (defer section) for details
              // eslint-disable-next-line react/jsx-no-constructed-context-values
              value={{ options: applicationOptions, state: applicationState }}
            >
              {isAgent ? (
                <Switch>
                  <Route
                    path="/system/:tab?"
                    render={(props) =>
                      this.withTokenCheck(
                        <System
                          {...props}
                          options={applicationOptions}
                          state={applicationState}
                        />,
                      )
                    }
                  />
                  <Redirect
                    from="*"
                    to="/system"
                  />
                </Switch>
              ) : (
                <Switch>
                  <Route
                    exact
                    path="/collections"
                    render={(props) => {
                      // This should log if route matches
                      if (typeof window !== 'undefined') {
                        window.routeMatchedCollections = true;
                        console.log(
                          '[Router] /collections route matched!',
                          props,
                        );
                      }

                      try {
                        const result = this.withTokenCheck(
                          <div className="view">
                            <Collections {...props} />
                          </div>,
                        );
                        console.log(
                          '[Router] Collections rendered successfully',
                        );
                        return result;
                      } catch (renderError) {
                        console.error(
                          '[Router] Error rendering Collections:',
                          renderError,
                        );
                        // Return error UI instead of crashing
                        return (
                          <div className="view">
                            <ErrorSegment
                              caption={`Error loading Collections: ${renderError.message}`}
                            />
                          </div>
                        );
                      }
                    }}
                  />
                  <Route
                    exact
                    path="/searches"
                    render={(props) =>
                      this.withTokenCheck(
                        <div className="view">
                          <Searches
                            server={applicationState.server}
                            {...props}
                          />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path="/searches/:id"
                    render={(props) =>
                      this.withTokenCheck(
                        <div className="view">
                          <Searches
                            server={applicationState.server}
                            {...props}
                          />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path="/wishlist"
                    render={(props) =>
                      this.withTokenCheck(
                        <div className="view">
                          <Wishlist {...props} />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path="/browse"
                    render={(props) =>
                      this.withTokenCheck(<Browse {...props} />)
                    }
                  />
                  <Route
                    path="/users"
                    render={(props) =>
                      this.withTokenCheck(<Users {...props} />)
                    }
                  />
                  <Route
                    exact
                    path="/contacts"
                    render={(props) =>
                      this.withTokenCheck(<Contacts {...props} />)
                    }
                  />
                  <Route
                    path="/sharegroups"
                    render={(props) =>
                      this.withTokenCheck(
                        <div className="view">
                          <ShareGroups {...props} />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path="/shared"
                    render={(props) =>
                      this.withTokenCheck(
                        <div className="view">
                          <SharedWithMe {...props} />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path="/chat"
                    render={(props) =>
                      this.withTokenCheck(
                        <Chat
                          {...props}
                          state={applicationState}
                        />,
                      )
                    }
                  />
                  <Route
                    path="/pods/:podId?/channels/:channelId?"
                    render={(props) => this.withTokenCheck(<Pods {...props} />)}
                  />
                  <Route
                    path="/rooms"
                    render={(props) =>
                      this.withTokenCheck(<Rooms {...props} />)
                    }
                  />
                  <Route
                    path="/uploads"
                    render={(props) =>
                      this.withTokenCheck(
                        <div className="view">
                          <Transfers
                            {...props}
                            direction="upload"
                          />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path="/downloads"
                    render={(props) =>
                      this.withTokenCheck(
                        <div className="view">
                          <Transfers
                            {...props}
                            direction="download"
                            server={applicationState.server}
                          />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path="/system/:tab?"
                    render={(props) =>
                      this.withTokenCheck(
                        <System
                          {...props}
                          options={applicationOptions}
                          state={applicationState}
                          theme={theme}
                        />,
                      )
                    }
                  />
                  <Route path="*">
                    {({ location }) => {
                      // Log route miss for debugging - this should appear in browser console
                      console.error(
                        '[Router] Route miss for:',
                        location.pathname,
                      );
                      // Set flag so test can detect route miss
                      if (typeof window !== 'undefined') {
                        window.routeMissPath = location.pathname;
                        // Also set it on the route-miss element so it persists even after redirect
                        setTimeout(() => {
                          const element = document.querySelector(
                            '[data-testid="route-miss"]',
                          );
                          if (element) {
                            window.routeMissElement = element.textContent;
                          }
                        }, 100);
                      }

                      return (
                        <>
                          <div
                            data-testid="route-miss"
                            style={{
                              background: 'red',
                              color: 'white',
                              left: 0,
                              padding: '20px',
                              position: 'fixed',
                              top: 0,
                              zIndex: 9_999,
                            }}
                          >
                            Route miss: {location.pathname}
                          </div>
                          <Redirect to="/searches" />
                        </>
                      );
                    }}
                  </Route>
                </Switch>
              )}
            </AppContext.Provider>
          </Sidebar.Pusher>
        </Sidebar.Pushable>
        <ToastContainer
          autoClose={5_000}
          closeOnClick
          draggable={false}
          hideProgressBar={false}
          newestOnTop
          pauseOnFocusLoss
          pauseOnHover
          position="bottom-center"
          rtl={false}
        />
        <Footer />
      </>
    );
  }
}

export default App;
