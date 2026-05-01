import Wishlist from './Wishlist';
import { discoveryInboxStorageKey } from '../../lib/discoveryInbox';
import * as spotifyIntegrationAPI from '../../lib/spotifyIntegration';
import * as wishlistAPI from '../../lib/wishlist';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';
import { MemoryRouter } from 'react-router-dom';

vi.mock('../../lib/wishlist', () => ({
  create: vi.fn(),
  getAll: vi.fn(),
  importCsv: vi.fn(),
  remove: vi.fn(),
  runSearch: vi.fn(),
  update: vi.fn(),
}));

vi.mock('../../lib/spotifyIntegration', () => ({
  disconnectSpotify: vi.fn(),
  getSpotifyStatus: vi.fn(),
  startSpotifyAuthorization: vi.fn(),
}));

const renderWishlist = () =>
  render(
    <MemoryRouter>
      <Wishlist />
    </MemoryRouter>,
  );

describe('Wishlist', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    Object.assign(navigator, {
      clipboard: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
    });
    spotifyIntegrationAPI.getSpotifyStatus.mockResolvedValue({
      configured: false,
      connected: false,
    });
    wishlistAPI.getAll.mockResolvedValue([
      {
        autoDownload: false,
        enabled: true,
        filter: 'flac',
        id: 'wish-1',
        lastMatchCount: 0,
        lastSearchedAt: null,
        searchText: 'rare album',
        totalSearchCount: 0,
      },
      {
        autoDownload: true,
        enabled: true,
        id: 'wish-2',
        lastMatchCount: 3,
        lastSearchedAt: '2026-04-30T19:30:00Z',
        searchText: 'auto track',
        totalSearchCount: 2,
      },
    ]);
  });

  it('shows unified request states for wishlist rows', async () => {
    renderWishlist();

    expect(await screen.findByText('rare album')).toBeInTheDocument();
    expect(screen.getByText('Wanted')).toBeInTheDocument();
    expect(screen.getAllByText('Automatic').length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText('Request Portal Summary')).toBeInTheDocument();
    expect(screen.getByText('23 left')).toBeInTheDocument();
  });

  it('promotes a wishlist row to Discovery Inbox review', async () => {
    renderWishlist();

    expect(await screen.findByText('rare album')).toBeInTheDocument();
    fireEvent.click(screen.getAllByTitle('Send to Discovery Inbox review')[0]);

    const persisted = JSON.parse(localStorage.getItem(discoveryInboxStorageKey));
    expect(persisted).toHaveLength(1);
    expect(persisted[0]).toEqual(
      expect.objectContaining({
        evidenceKey: 'wishlist:wish-1:rare album:flac',
        searchText: 'rare album',
        source: 'Wishlist',
        sourceId: 'wish-1',
        state: 'Suggested',
      }),
    );

    await waitFor(() => expect(screen.getByText('Review')).toBeInTheDocument());
  });

  it('copies a wishlist request review packet', async () => {
    renderWishlist();

    expect(await screen.findByText('rare album')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Copy Wishlist request review' }));

    await waitFor(() => {
      expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
        expect.stringContaining('slskdN Wishlist request review'),
      );
    });
  });

  it('runs enabled wishlist searches in a bounded batch', async () => {
    wishlistAPI.runSearch.mockResolvedValue({ responseCount: 4 });

    renderWishlist();

    expect(await screen.findByText('rare album')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Run enabled Wishlist searches' }));

    await waitFor(() => {
      expect(wishlistAPI.runSearch).toHaveBeenCalledTimes(2);
    });
    expect(wishlistAPI.runSearch).toHaveBeenCalledWith('wish-1');
    expect(wishlistAPI.runSearch).toHaveBeenCalledWith('wish-2');
    expect(screen.getByText(/Ran 2 enabled Wishlist searches/)).toBeInTheDocument();
  });
});
