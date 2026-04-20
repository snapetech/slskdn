import '@testing-library/jest-dom';
import App from './App';
import React from 'react';
import { MemoryRouter } from 'react-router-dom';
import { render, screen, waitFor } from '@testing-library/react';
import { vi } from 'vitest';

const {
  check,
  createApplicationHubConnection,
  getSecurityEnabled,
  isLoggedIn,
} = vi.hoisted(() => ({
  check: vi.fn(),
  createApplicationHubConnection: vi.fn(),
  getSecurityEnabled: vi.fn(),
  isLoggedIn: vi.fn(),
}));

vi.mock('../lib/hubFactory', () => ({
  createApplicationHubConnection,
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
vi.mock('./Shared', () => ({
  isStatusBarVisible: vi.fn(() => false),
  SlskdnStatusBar: () => <div>Status Bar</div>,
  toggleStatusBarVisibility: vi.fn(() => false),
}));
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

describe('App', () => {
  beforeEach(() => {
    const hub = {
      on: vi.fn(),
      onclose: vi.fn(),
      onreconnected: vi.fn(),
      onreconnecting: vi.fn(),
      start: vi.fn(() => new Promise(() => {})),
      stop: vi.fn(() => Promise.resolve()),
    };

    createApplicationHubConnection.mockReturnValue(hub);
    getSecurityEnabled.mockResolvedValue(true);
    check.mockResolvedValue(true);
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
});
