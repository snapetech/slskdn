import 'react-toastify/dist/ReactToastify.css';
import './App.css';
import { createApplicationHubConnection } from '../lib/hubFactory';
import * as relayAPI from '../lib/relay';
import { connect, disconnect } from '../lib/server';
import * as session from '../lib/session';
import { isPassthroughEnabled } from '../lib/token';
import AppContext from './AppContext';
import LoginForm from './LoginForm';
import PlayerBar from './Player/PlayerBar';
import { PlayerProvider } from './Player/PlayerContext';
import ErrorSegment from './Shared/ErrorSegment';
import Footer from './Shared/Footer';
import React, { Component, lazy, Suspense } from 'react';
import { Link, Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { ToastContainer } from 'react-toastify';
import {
  Button,
  Header,
  Icon,
  Loader,
  Menu,
  Modal,
  Popup,
  Segment,
  Sidebar,
} from 'semantic-ui-react';

const SLSKDN_RELEASES_URL = 'https://github.com/snapetech/slskdn/releases';

const Browse = lazy(() => import('./Browse/Browse'));
const Chat = lazy(() => import('./Chat/Chat'));
const Collections = lazy(() => import('./Collections/Collections'));
const Contacts = lazy(() => import('./Contacts/Contacts'));
const DiscoveryGraphAtlasPage = lazy(() =>
  import('./Search/DiscoveryGraphAtlasPage'));
const Pods = lazy(() => import('./Pods/Pods'));
const Rooms = lazy(() => import('./Rooms/Rooms'));
const Searches = lazy(() => import('./Search/Searches'));
const ShareGroups = lazy(() => import('./ShareGroups/ShareGroups'));
const SharedWithMe = lazy(() => import('./Shares/SharedWithMe'));
const SolidSettings = lazy(() => import('./Solid/SolidSettings'));
const System = lazy(() => import('./System/System'));
const Transfers = lazy(() => import('./Transfers/Transfers'));
const Users = lazy(() => import('./Users/Users'));
const Wishlist = lazy(() => import('./Wishlist/Wishlist'));

const THEME_OPTIONS = [
  { key: 'slskdn', text: 'slskdN', value: 'slskdn' },
  { key: 'classic-dark', text: 'Classic Dark', value: 'classic-dark' },
  { key: 'light', text: 'Light', value: 'light' },
];

const THEME_LABELS = THEME_OPTIONS.reduce(
  (labels, option) => ({ ...labels, [option.value]: option.text }),
  {},
);

const normalizeTheme = (theme) => {
  if (theme === 'light' || theme === 'classic-dark') {
    return theme;
  }

  return 'slskdn';
};

const getSemanticTheme = (theme) => (theme === 'light' ? 'light' : 'dark');

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
  themeMenuOpen: false,
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

const RouteMissRedirect = () => {
  const location = useLocation();

  if (typeof window !== 'undefined') {
    window.routeMissPath = location.pathname;

    setTimeout(() => {
      const element = document.querySelector('[data-testid="route-miss"]');
      if (element) {
        window.routeMissElement = element.textContent;
      }
    }, 100);
  }

  console.error('[Router] Route miss for:', location.pathname);

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
      <Navigate replace to="/searches" />
    </>
  );
};

class App extends Component {
  constructor(props) {
    super(props);

    this.state = initialState;
    this.applicationHub = undefined;
  }

  componentDidMount() {
    this.init();
  }

  componentWillUnmount() {
    if (this.applicationHub) {
      this.applicationHub.stop().catch(() => {});
      this.applicationHub = undefined;
    }
  }

  startApplicationHub = () => {
    if (this.applicationHub) {
      this.applicationHub.stop().catch(() => {});
    }

    const HUB_START_TIMEOUT_MS = 30000;
    const appHub = createApplicationHubConnection();
    this.applicationHub = appHub;

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

    const hubStart = appHub.start();
    let hubTimeoutId;
    const hubTimeout = new Promise((_, reject) => {
      hubTimeoutId = setTimeout(
        () => reject(new Error('HubConnectionTimeout')),
        HUB_START_TIMEOUT_MS,
      );
    });

    Promise.race([hubStart, hubTimeout])
      .catch((error) => {
        if (this.applicationHub !== appHub) {
          return;
        }

        if (error?.message === 'HubConnectionTimeout') {
          console.warn(
            'Hub connection timed out during background startup; allowing the UI to continue while SignalR retries.',
          );
          return;
        }

        console.error(error);
        this.setState({ error: true, retriesExhausted: false });
      })
      .finally(() => {
        if (hubTimeoutId) {
          clearTimeout(hubTimeoutId);
        }

        // Prevent unhandled rejections if the timeout wins and the start later faults.
        hubStart.catch(() => {});
      });
  };

  init = async () => {
    this.setState({ initialized: false }, async () => {
      const INIT_TOTAL_TIMEOUT_MS = 30000;

      let initTimedOut = false;
      let initTimeoutId;
      try {
        const initTask = (async () => {
          const securityEnabled = await session.getSecurityEnabled();

          if (!securityEnabled) {
            console.debug('application security is not enabled, per api call');
            session.enablePassthrough();
          }

          if (await session.check()) {
            this.startApplicationHub();
          }

          const savedTheme = this.getSavedTheme();
          if (savedTheme != null) {
            this.setState({ theme: savedTheme });
          }

          this.setState({
            error: false,
          });
        })();

        // Safety timeout so a stalled init doesn't keep the UI on the big loader forever.
        const initTimeout = new Promise((resolve) => {
          initTimeoutId = setTimeout(() => {
            initTimedOut = true;
            resolve();
          }, INIT_TOTAL_TIMEOUT_MS);
        });

        await Promise.race([initTask, initTimeout]);

        // Prevent unhandled rejections if the timeout wins.
        initTask.catch((error) => {
          if (initTimedOut) {
            console.warn('Init completed after timeout.', error);
          }
        });

        if (initTimedOut) {
          console.warn('Init timed out; showing UI (hub/state may reconnect later).');
        }
      } catch (error) {
        if (!initTimedOut) {
          console.error(error);
          this.setState({ error: true, retriesExhausted: true });
        }
      } finally {
        if (initTimeoutId) {
          clearTimeout(initTimeoutId);
        }
        this.setState({ initialized: true });
      }
    });
  };

  getSavedTheme = () => {
    const savedTheme = localStorage.getItem('slskd-theme');
    return savedTheme == null ? null : normalizeTheme(savedTheme);
  };

  setTheme = (theme) => {
    const nextTheme = normalizeTheme(theme);
    localStorage.setItem('slskd-theme', nextTheme);
    this.setState({ theme: nextTheme, themeMenuOpen: false });
  };

  openThemeMenu = () => {
    this.setState({ themeMenuOpen: true });
  };

  closeThemeMenu = () => {
    this.setState({ themeMenuOpen: false });
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
    return component;
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
      theme = normalizeTheme(this.getSavedTheme() || 'slskdn'),
      themeMenuOpen,
    } = this.state;
    const semanticTheme = getSemanticTheme(theme);
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

    if (!session.isLoggedIn() && !isPassthroughEnabled()) {
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
    document.title = 'slskdN';

    document.documentElement.classList.remove(
      'classic-dark',
      'dark',
      'light',
      'slskdn',
    );
    document.documentElement.classList.add(theme);
    if (semanticTheme === 'dark') {
      document.documentElement.classList.add('dark');
    }

    return (
      <>
        {error && (
          <Segment
            color="red"
            inverted
            style={{
              borderRadius: 0,
              margin: 0,
              padding: '0.75rem 1rem',
            }}
          >
            <Icon name="attention" />
            Lost connection to slskd. {retriesExhausted ? 'Refresh to reconnect.' : 'Retrying...'}
          </Segment>
        )}
        <PlayerProvider>
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
              <div className="navigation-primary">
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
                  <Link to="/discovery-graph">
                    <Menu.Item data-testid="nav-discovery-graph">
                      <Icon name="crosshairs" />
                      Discovery Graph
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
                  <Link to="/solid">
                    <Menu.Item data-testid="nav-solid">
                      <Icon name="key" />
                      Solid
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
            </div>
            <Menu
              className="right"
              inverted
            >
              <ModeSpecificConnectButton
                connectionWatchdog={connectionWatchdog}
                controller={controller}
                mode={mode}
                pendingReconnect={pendingReconnect}
                server={server}
                user={user}
              />
              <Popup
                basic
                className="theme-picker-popup"
                on="click"
                onClose={this.closeThemeMenu}
                onOpen={this.openThemeMenu}
                open={themeMenuOpen}
                pinned
                position="bottom right"
                trigger={(
                  <Menu.Item
                    className={`theme-menu ${themeMenuOpen ? 'visible' : ''}`}
                    data-testid="theme-menu"
                    title="Choose the web UI color theme"
                  >
                    <Icon name="paint brush" />
                    <span className="theme-menu-label">Theme</span>
                  </Menu.Item>
                )}
              >
                <Menu
                  className="theme-picker-menu"
                  vertical
                >
                  {THEME_OPTIONS.map((option) => (
                    <Menu.Item
                      active={theme === option.value}
                      data-testid={`theme-option-${option.value}`}
                      key={option.value}
                      onClick={() => this.setTheme(option.value)}
                    >
                      <Icon name="theme" />
                      {option.text}
                    </Menu.Item>
                  ))}
                </Menu>
              </Popup>
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
                      href={SLSKDN_RELEASES_URL}
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
              <AppContext.Provider
                // Note: Context value object recreated on each render (class component limitation)
                // Deferred: Optimize with useMemo when converting to functional component
                // See memory-bank/triage-todo-fixme.md (defer section) for details
                // eslint-disable-next-line react/jsx-no-constructed-context-values
                value={{ options: applicationOptions, state: applicationState }}
              >
                <Suspense
                  fallback={
                    <Segment
                      basic
                      className="view"
                    >
                      <Loader active />
                    </Segment>
                  }
                >
                  {isAgent ? (
                  <Routes>
                  <Route
                    path="/system"
                    element={
                      this.withTokenCheck(
                        <System
                          options={applicationOptions}
                          state={applicationState}
                        />,
                      )
                    }
                  />
                  <Route
                    path="/system/:tab"
                    element={
                      this.withTokenCheck(
                        <System
                          options={applicationOptions}
                          state={applicationState}
                        />,
                      )
                    }
                  />
                  <Route
                    path="*"
                    element={<Navigate replace to="/system" />}
                  />
                  </Routes>
                  ) : (
                  <Routes>
                  <Route
                    path="/"
                    element={<Navigate replace to="/searches" />}
                  />
                  <Route
                    path="/collections"
                    element={(() => {
                      // This should log if route matches
                      if (typeof window !== 'undefined') {
                        window.routeMatchedCollections = true;
                        console.log(
                          '[Router] /collections route matched!',
                          '/collections',
                        );
                      }

                      try {
                        const result = this.withTokenCheck(
                          <div className="view">
                            <Collections />
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
                    })()}
                  />
                  <Route
                    path="/solid"
                    element={
                      this.withTokenCheck(
                        <div className="view">
                          <SolidSettings />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path="/discovery-graph"
                    element={
                      this.withTokenCheck(
                        <DiscoveryGraphAtlasPage
                          server={applicationState.server}
                        />,
                      )
                    }
                  />
                  <Route
                    path="/searches"
                    element={
                      this.withTokenCheck(
                        <div className="view">
                          <Searches server={applicationState.server} />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path="/searches/:id"
                    element={
                      this.withTokenCheck(
                        <div className="view">
                          <Searches server={applicationState.server} />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path="/wishlist"
                    element={
                      this.withTokenCheck(
                        <div className="view">
                          <Wishlist />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path="/browse"
                    element={this.withTokenCheck(<Browse />)}
                  />
                  <Route
                    path="/users"
                    element={this.withTokenCheck(<Users />)}
                  />
                  <Route
                    path="/contacts"
                    element={this.withTokenCheck(<Contacts />)}
                  />
                  <Route
                    path="/sharegroups"
                    element={
                      this.withTokenCheck(
                        <div className="view">
                          <ShareGroups />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path="/shared"
                    element={
                      this.withTokenCheck(
                        <div className="view">
                          <SharedWithMe />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path="/chat"
                    element={
                      this.withTokenCheck(
                        <Chat
                          state={applicationState}
                        />,
                      )
                    }
                  />
                  <Route
                    path="/pods"
                    element={this.withTokenCheck(<Pods state={applicationState} />)}
                  />
                  <Route
                    path="/pods/:podId"
                    element={this.withTokenCheck(<Pods state={applicationState} />)}
                  />
                  <Route
                    path="/pods/:podId/channels/:channelId"
                    element={this.withTokenCheck(<Pods state={applicationState} />)}
                  />
                  <Route
                    path="/rooms"
                    element={this.withTokenCheck(<Rooms />)}
                  />
                  <Route
                    path="/uploads"
                    element={
                      this.withTokenCheck(
                        <div className="view">
                          <Transfers
                            direction="upload"
                          />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path="/downloads"
                    element={
                      this.withTokenCheck(
                        <div className="view">
                          <Transfers
                            direction="download"
                            server={applicationState.server}
                          />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path="/system"
                    element={
                      this.withTokenCheck(
                        <System
                          options={applicationOptions}
                          state={applicationState}
                          theme={semanticTheme}
                        />,
                      )
                    }
                  />
                  <Route
                    path="/system/:tab"
                    element={
                      this.withTokenCheck(
                        <System
                          options={applicationOptions}
                          state={applicationState}
                          theme={semanticTheme}
                        />,
                      )
                    }
                  />
                  <Route
                    path="*"
                    element={<RouteMissRedirect />}
                  />
                  </Routes>
                  )}
                </Suspense>
              </AppContext.Provider>
            </Sidebar.Pusher>
          </Sidebar.Pushable>
          <PlayerBar />
        </PlayerProvider>
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
