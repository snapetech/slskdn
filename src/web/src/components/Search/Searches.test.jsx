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
vi.mock('./DiscoveryGraphAtlasPanel', () => ({ default: () => null }));
vi.mock('./MusicBrainzLookup', () => ({ default: () => null }));
vi.mock('./SongIDPanel', () => ({ default: () => null }));
vi.mock('./Detail/SearchDetail', () => ({ default: () => null }));
vi.mock('./List/SearchList', () => ({ default: () => null }));

const callbacks = {};

const renderSearches = async () => {
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

  return screen.findByTestId('search-input');
};

describe('Searches', () => {
  beforeEach(() => {
    vi.clearAllMocks();
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
        providers: ['pod', 'scene'],
        searchText: 'beatles',
      }),
    );
  });
});
