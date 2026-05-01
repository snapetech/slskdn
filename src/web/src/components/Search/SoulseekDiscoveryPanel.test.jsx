// <copyright file="SoulseekDiscoveryPanel.test.jsx" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import SoulseekDiscoveryPanel from './SoulseekDiscoveryPanel';
import * as soulseekDiscovery from '../../lib/soulseekDiscovery';
import * as wishlist from '../../lib/wishlist';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('../../lib/soulseekDiscovery', () => ({
  addHatedInterest: vi.fn(),
  addInterest: vi.fn(),
  getGlobalRecommendations: vi.fn(),
  getItemRecommendations: vi.fn(),
  getItemSimilarUsers: vi.fn(),
  getRecommendations: vi.fn(),
  getSimilarUsers: vi.fn(),
  getUserInterests: vi.fn(),
  removeHatedInterest: vi.fn(),
  removeInterest: vi.fn(),
}));

vi.mock('../../lib/wishlist', () => ({
  create: vi.fn(),
}));

describe('SoulseekDiscoveryPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    soulseekDiscovery.getRecommendations.mockResolvedValue({
      data: {
        recommendations: [{ item: 'Deep Dub', score: 42 }],
        unrecommendations: [],
      },
    });
    soulseekDiscovery.getSimilarUsers.mockResolvedValue({
      data: [{ rating: 9, username: 'taste-peer' }],
    });
    soulseekDiscovery.getUserInterests.mockResolvedValue({
      data: {
        hated: ['noise'],
        liked: ['dub'],
      },
    });
    wishlist.create.mockResolvedValue({});
  });

  it('loads native recommendations and hands them to search and Wishlist', async () => {
    const onSearch = vi.fn();
    render(<SoulseekDiscoveryPanel onSearch={onSearch} />);

    fireEvent.click(screen.getByRole('button', { name: 'My Recs' }));

    expect(await screen.findByText('Deep Dub')).toBeInTheDocument();
    fireEvent.click(screen.getByLabelText('Search Deep Dub'));
    expect(onSearch).toHaveBeenCalledWith('Deep Dub');

    fireEvent.click(screen.getByLabelText('Add Deep Dub to Wishlist'));
    await waitFor(() =>
      expect(wishlist.create).toHaveBeenCalledWith(
        expect.objectContaining({
          enabled: false,
          searchText: 'Deep Dub',
        }),
      ),
    );
  });

  it('loads similar users and then user interests on demand', async () => {
    render(<SoulseekDiscoveryPanel />);

    fireEvent.click(screen.getByRole('button', { name: 'Similar Users' }));

    expect(await screen.findByText('taste-peer')).toBeInTheDocument();
    fireEvent.click(screen.getByLabelText('Load taste-peer interests'));

    expect(await screen.findByText('dub')).toBeInTheDocument();
    expect(screen.getByText('noise')).toBeInTheDocument();
    expect(soulseekDiscovery.getUserInterests).toHaveBeenCalledWith({
      username: 'taste-peer',
    });
  });
});
