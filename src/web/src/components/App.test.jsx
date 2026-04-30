import '@testing-library/jest-dom';
import App from './App';
import React from 'react';
import { MemoryRouter } from 'react-router-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { vi } from 'vitest';

const {
  check,
  createApplicationHubConnection,
  getSecurityEnabled,
  getConversations,
  getJoinedRooms,
  getRoomMessages,
  isLoggedIn,
} = vi.hoisted(() => ({
  check: vi.fn(),
  createApplicationHubConnection: vi.fn(),
  getConversations: vi.fn(),
  getSecurityEnabled: vi.fn(),
  getJoinedRooms: vi.fn(),
  getRoomMessages: vi.fn(),
  isLoggedIn: vi.fn(),
}));

vi.mock('../lib/chat', () => ({
  getAll: getConversations,
}));

vi.mock('../lib/hubFactory', () => ({
  createApplicationHubConnection,
}));

vi.mock('../lib/rooms', () => ({
  getJoined: getJoinedRooms,
  getMessages: getRoomMessages,
}));

vi.mock('../lib/session', () => ({
  check,
  getSecurityEnabled,
  isLoggedIn,
  login: vi.fn(),
  logout: vi.fn(),
}));

vi.mock('../lib/token', () => ({
  isPassthroughEnabled: vi.fn(() => false),
}));

vi.mock('../lib/relay', () => ({
  connect: vi.fn(),
  disconnect: vi.fn(),
}));

vi.mock('../lib/server', () => ({
  connect: vi.fn(),
  disconnect: vi.fn(),
}));

vi.mock('./Browse/Browse', () => ({ default: () => <div>Browse</div> }));
vi.mock('./Chat/Chat', () => ({ default: () => <div>Chat</div> }));
vi.mock('./Collections/Collections', () => ({
  default: () => <div>Collections</div>,
}));
vi.mock('./Contacts/Contacts', () => ({ default: () => <div>Contacts</div> }));
vi.mock('./Search/DiscoveryGraphAtlasPage', () => ({
  default: () => <div>Discovery Graph</div>,
}));
vi.mock('./LoginForm', () => ({ default: () => <div>Login Form</div> }));
vi.mock('./Pods/Pods', () => ({ default: () => <div>Pods</div> }));
vi.mock('./Rooms/Rooms', () => ({ default: () => <div>Rooms</div> }));
vi.mock('./Search/Searches', () => ({ default: () => <div>Searches</div> }));
vi.mock('./Shared/ErrorSegment', () => ({
  default: ({ caption }) => <div>{caption}</div>,
}));
vi.mock('./Shared/Footer', () => ({ default: () => <div>Footer</div> }));
vi.mock('./ShareGroups/ShareGroups', () => ({
  default: () => <div>Share Groups</div>,
}));
vi.mock('./Shares/SharedWithMe', () => ({
  default: () => <div>Shared With Me</div>,
}));
vi.mock('./Solid/SolidSettings', () => ({
  default: () => <div>Solid</div>,
}));
vi.mock('./System/System', () => ({ default: () => <div>System</div> }));
vi.mock('./Transfers/Transfers', () => ({
  default: () => <div>Transfers</div>,
}));
vi.mock('./Users/Users', () => ({ default: () => <div>Users</div> }));
vi.mock('./Wishlist/Wishlist', () => ({ default: () => <div>Wishlist</div> }));

let hubHandlers;

describe('App', () => {
  beforeEach(() => {
    hubHandlers = {};
    const hub = {
      on: vi.fn((event, handler) => {
        hubHandlers[event] = handler;
      }),
      onclose: vi.fn(),
      onreconnected: vi.fn(),
      onreconnecting: vi.fn(),
      start: vi.fn(() => new Promise(() => {})),
      stop: vi.fn(() => Promise.resolve()),
    };

    createApplicationHubConnection.mockReturnValue(hub);
    getSecurityEnabled.mockResolvedValue(true);
    check.mockResolvedValue(true);
    getConversations.mockResolvedValue([]);
    getJoinedRooms.mockResolvedValue([]);
    getRoomMessages.mockResolvedValue([]);
    isLoggedIn.mockReturnValue(true);

    window.matchMedia = vi.fn().mockReturnValue({
      addEventListener: vi.fn(),
      matches: false,
      removeEventListener: vi.fn(),
    });
    localStorage.clear();
    sessionStorage.clear();
  });

  afterEach(() => {
    vi.clearAllMocks();
    document.documentElement.className = '';
  });

  it('redirects the root route to searches without logging a route miss', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});

    render(
      <MemoryRouter initialEntries={['/']}>
        <App />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByText('Searches')).toBeInTheDocument();
    });

    expect(consoleError).not.toHaveBeenCalledWith('[Router] Route miss for:', '/');
  });

  it('does not keep the initial loader visible while the app hub startup stalls', async () => {
    const { container } = render(
      <MemoryRouter>
        <App />
      </MemoryRouter>,
    );

    expect(container.querySelector('.ui.active.loader')).toBeInTheDocument();

    await waitFor(() => {
      expect(container.querySelector('.ui.active.loader')).not.toBeInTheDocument();
    });

    expect(createApplicationHubConnection).toHaveBeenCalledTimes(1);
    expect(check).toHaveBeenCalled();
  });

  it('opens the theme menu and applies the selected theme', async () => {
    render(
      <MemoryRouter>
        <App />
      </MemoryRouter>,
    );

    const themeMenu = await screen.findByTestId('theme-menu');
    fireEvent.click(themeMenu);
    fireEvent.click(await screen.findByText('Light'));

    await waitFor(() => {
      expect(localStorage.getItem('slskd-theme')).toBe('light');
      expect(document.documentElement).toHaveClass('light');
    });
  });

  it('keeps the browser tab title focused on slskdN branding', async () => {
    render(
      <MemoryRouter>
        <App />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByText('Searches')).toBeInTheDocument();
    });

    expect(document.title).toBe('slskdN');
  });

  it('shows chat activity in the header when conversations have unread messages', async () => {
    getConversations.mockResolvedValue([
      {
        hasUnAcknowledgedMessages: true,
        username: 'some-user',
      },
    ]);

    render(
      <MemoryRouter initialEntries={['/searches']}>
        <App />
      </MemoryRouter>,
    );

    expect(await screen.findByTestId('nav-chat-alert')).toBeInTheDocument();
    expect(getConversations).toHaveBeenCalledWith({ unAcknowledgedOnly: true });
  });

  it('shows room activity in the header when joined rooms have newer incoming messages', async () => {
    localStorage.setItem(
      'slskdn.rooms.lastSeenActivity',
      JSON.stringify({ chill: Date.parse('2026-04-30T00:00:00Z') }),
    );
    getJoinedRooms.mockResolvedValue(['chill']);
    getRoomMessages.mockResolvedValue([
      {
        message: 'new one',
        self: false,
        timestamp: '2026-04-30T00:01:00Z',
        username: 'friend',
      },
    ]);

    render(
      <MemoryRouter initialEntries={['/searches']}>
        <App />
      </MemoryRouter>,
    );

    expect(await screen.findByTestId('nav-rooms-alert')).toBeInTheDocument();
  });

  it('shows a dismissible VPN forwarded port notice when VPN ports are reported', async () => {
    render(
      <MemoryRouter>
        <App />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByText('Searches')).toBeInTheDocument();
    });

    hubHandlers.state({
      server: { isConnected: true },
      vpn: {
        isReady: true,
        portForwards: [
          {
            localPort: 50300,
            proto: 'tcp',
            publicIPAddress: '203.0.113.10',
            publicPort: 51000,
            slot: 0,
            targetPort: 51000,
          },
          {
            localPort: 50305,
            proto: 'udp',
            publicIPAddress: '203.0.113.20',
            publicPort: 51001,
            slot: 1,
            targetPort: 50305,
          },
        ],
      },
    });

    expect(
      await screen.findByTestId('vpn-port-change-notice'),
    ).toBeInTheDocument();
    expect(screen.getByText(/203\.0\.113\.10:51000/)).toBeInTheDocument();
    expect(screen.getByText(/203\.0\.113\.20:51001 -> 50305/)).toBeInTheDocument();

    fireEvent.click(screen.getByTitle('Dismiss VPN port notice'));

    await waitFor(() => {
      expect(
        screen.queryByTestId('vpn-port-change-notice'),
      ).not.toBeInTheDocument();
    });
  });
});
