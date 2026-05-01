// <copyright file="Searches.test.jsx" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import Searches from './Searches';
import { createSearchHubConnection } from '../../lib/hubFactory';
import { getCapabilities } from '../../lib/slskdn';
import * as library from '../../lib/searches';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';
import { MemoryRouter } from 'react-router-dom';

vi.mock('../../lib/hubFactory', () => ({
  createSearchHubConnection: vi.fn(),
}));
vi.mock('../../lib/slskdn', () => ({
  getCapabilities: vi.fn(),
}));
vi.mock('../../lib/searches', () => ({
  create: vi.fn(),
  getAll: vi.fn(),
  remove: vi.fn(),
  removeAll: vi.fn(),
  stop: vi.fn(),
}));
vi.mock('./AlbumCompletionPanel', () => ({ default: () => null }));
vi.mock('./ArtistReleaseRadarPanel', () => ({ default: () => null }));
vi.mock('./DiscoveryGraphAtlasPanel', () => ({ default: () => null }));
vi.mock('./FederatedTasteRecommendationsPanel', () => ({ default: () => null }));
vi.mock('./MusicBrainzLookup', () => ({ default: () => null }));
vi.mock('./SongIDPanel', () => ({
  default: () => <div data-testid="songid-panel">SongID panel</div>,
}));
vi.mock('./Detail/SearchDetail', () => ({ default: () => null }));
vi.mock('./List/SearchList', () => ({ default: () => null }));

const callbacks = {};

const renderSearches = async ({ waitForInput = true } = {}) => {
  callbacks.list = undefined;
  createSearchHubConnection.mockReturnValue({
    on: vi.fn((eventName, callback) => {
      callbacks[eventName] = callback;
    }),
    onclose: vi.fn(),
    onreconnected: vi.fn(),
    onreconnecting: vi.fn(),
    start: vi.fn(async () => {
      callbacks.list?.([]);
    }),
    stop: vi.fn(),
  });

  render(
    <MemoryRouter initialEntries={['/searches']}>
      <Searches server={{ isConnected: true }} />
    </MemoryRouter>,
  );

  return waitForInput ? screen.findByTestId('search-input') : undefined;
};

describe('Searches', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    getCapabilities.mockResolvedValue({ features: [] });
    library.create.mockResolvedValue({});
  });

  it('keeps ScenePodBridge disabled by default and creates ordinary searches without providers', async () => {
    const input = await renderSearches();

    expect(screen.queryByText('Search Sources:')).not.toBeInTheDocument();

    fireEvent.change(input, { target: { value: 'beatles' } });
    fireEvent.keyUp(input, { key: 'Enter' });

    await waitFor(() => expect(library.create).toHaveBeenCalledTimes(1));
    expect(library.create).toHaveBeenCalledWith(
      expect.objectContaining({
        acquisitionProfile: 'lossless-exact',
        providers: null,
        searchText: 'beatles',
      }),
    );
  });

  it('only sends bridge providers when the backend explicitly advertises ScenePodBridge', async () => {
    getCapabilities.mockResolvedValue({
      feature: { scenePodBridge: true },
      features: ['scene_pod_bridge'],
    });
    const input = await renderSearches();

    expect(await screen.findByText('Search Sources:')).toBeInTheDocument();

    fireEvent.change(input, { target: { value: 'beatles' } });
    fireEvent.keyUp(input, { key: 'Enter' });

    await waitFor(() => expect(library.create).toHaveBeenCalledTimes(1));
    expect(library.create).toHaveBeenCalledWith(
      expect.objectContaining({
        acquisitionProfile: 'lossless-exact',
        providers: ['pod', 'scene'],
        searchText: 'beatles',
      }),
    );
  });

  it('defaults secondary search sections closed and remembers expanded state', async () => {
    await renderSearches();

    expect(screen.getByRole('button', { name: 'Expand SongID' })).toBeInTheDocument();
    expect(screen.queryByTestId('songid-panel')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Expand SongID' }));

    expect(screen.getByTestId('songid-panel')).toBeInTheDocument();
    expect(localStorage.getItem('slskdn.search.section.songid')).toBe('open');
  });

  it('shows and persists the selected acquisition profile', async () => {
    await renderSearches();

    expect(screen.getByText('Acquisition Profile')).toBeInTheDocument();
    expect(screen.getAllByText('Lossless Exact').length).toBeGreaterThan(0);

    fireEvent.click(screen.getByTestId('acquisition-profile-select'));
    fireEvent.click(screen.getByText('Conservative Network'));

    expect(localStorage.getItem('slskdn.acquisitionProfile')).toBe(
      'conservative-network',
    );
    expect(
      screen.getAllByText('Lower concurrency, no automatic public-peer retries.')
        .length,
    ).toBeGreaterThan(0);

    const input = screen.getByTestId('search-input');
    fireEvent.change(input, { target: { value: 'rare live set' } });
    fireEvent.keyUp(input, { key: 'Enter' });

    await waitFor(() => expect(library.create).toHaveBeenCalledTimes(1));
    expect(library.create).toHaveBeenCalledWith(
      expect.objectContaining({
        acquisitionProfile: 'conservative-network',
        searchText: 'rare live set',
      }),
    );
  });

  it('uses stored collapsed state for primary search sections', async () => {
    localStorage.setItem('slskdn.search.section.search', 'closed');

    await renderSearches({ waitForInput: false });

    expect(screen.getByRole('button', { name: 'Expand Search' })).toBeInTheDocument();
    expect(screen.queryByTestId('search-input')).not.toBeInTheDocument();
  });

  it('keeps manual search out of acquisition review', async () => {
    await renderSearches();

    expect(
      screen.queryByRole('button', { name: 'Add search phrase to Discovery Inbox' }),
    ).not.toBeInTheDocument();
  });
});
