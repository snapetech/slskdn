import DiscoveryInbox from './DiscoveryInbox';
import { acquisitionPlanStorageKey } from '../../lib/acquisitionPlans';
import {
  addDiscoveryInboxItem,
  discoveryInboxStorageKey,
} from '../../lib/discoveryInbox';
import { create as createSearch } from '../../lib/searches';
import { create as createWishlist } from '../../lib/wishlist';
import { saveWatchlist, watchlistStorageKey } from '../../lib/watchlists';
import { fireEvent, render, screen } from '@testing-library/react';
import React from 'react';
import { vi } from 'vitest';

vi.mock('../../lib/searches', () => ({
  create: vi.fn().mockResolvedValue({ data: {} }),
}));

vi.mock('../../lib/wishlist', () => ({
  create: vi.fn().mockResolvedValue({ id: 'wishlist-plan' }),
}));

describe('DiscoveryInbox', () => {
  beforeEach(() => {
    localStorage.clear();
    createSearch.mockClear();
    createWishlist.mockClear();
  });

  it('shows saved candidates and persists review decisions', () => {
    const item = addDiscoveryInboxItem({
      acquisitionProfile: 'rare-hunt',
      evidenceKey: 'manual-search:rare-track',
      networkImpact: 'Manual review only.',
      reason: 'Saved from Search while using Rare Hunt.',
      searchText: 'rare track',
      source: 'Search',
      title: 'rare track',
    });

    render(<DiscoveryInbox />);

    expect(screen.getByText('rare track')).toBeInTheDocument();
    expect(screen.getAllByText(/Rare Hunt/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Manual review only/).length).toBeGreaterThan(0);
    expect(screen.getByText('Review impact')).toBeInTheDocument();
    expect(screen.getAllByText('Local/manual').length).toBeGreaterThan(0);

    fireEvent.click(screen.getByRole('button', { name: 'Approve rare track' }));

    const persisted = JSON.parse(localStorage.getItem(discoveryInboxStorageKey));
    expect(persisted.find((candidate) => candidate.id === item.id).state).toBe(
      'Approved',
    );
    expect(screen.getAllByText('Approved').length).toBeGreaterThan(0);
  });

  it('snoozes candidates with a visible due date and can return them to review', () => {
    const item = addDiscoveryInboxItem({
      evidenceKey: 'manual-search:snooze-me',
      searchText: 'snooze me',
      source: 'Search',
      title: 'snooze me',
    });

    render(<DiscoveryInbox />);

    fireEvent.click(screen.getByRole('button', { name: 'Snooze snooze me' }));

    let persisted = JSON.parse(localStorage.getItem(discoveryInboxStorageKey));
    expect(persisted.find((candidate) => candidate.id === item.id)).toEqual(
      expect.objectContaining({
        state: 'Snoozed',
      }),
    );
    expect(screen.getByText('Snoozed until')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Unsnooze snooze me' }));

    persisted = JSON.parse(localStorage.getItem(discoveryInboxStorageKey));
    expect(persisted.find((candidate) => candidate.id === item.id)).toEqual(
      expect.objectContaining({
        snoozedUntil: '',
        state: 'Suggested',
      }),
    );
  });

  it('summarizes provider and network-risk review impact before approval', () => {
    addDiscoveryInboxItem({
      evidenceKey: 'provider:item',
      networkImpact: 'Provider metadata lookup required before planning.',
      searchText: 'provider item',
      source: 'Release Radar',
    });
    addDiscoveryInboxItem({
      evidenceKey: 'network:item',
      networkImpact: 'Automatic download would contact peers.',
      searchText: 'network item',
      source: 'Automation',
    });

    render(<DiscoveryInbox />);

    expect(screen.getAllByText('Provider review').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Network risk').length).toBeGreaterThan(0);
    expect(screen.getByText('review')).toBeInTheDocument();
  });

  it('bulk rejects suggested candidates', () => {
    addDiscoveryInboxItem({
      evidenceKey: 'manual-search:first',
      searchText: 'first',
      source: 'Search',
    });
    addDiscoveryInboxItem({
      evidenceKey: 'manual-search:second',
      searchText: 'second',
      source: 'Search',
    });

    render(<DiscoveryInbox />);

    fireEvent.click(
      screen.getByRole('button', { name: 'Reject suggested discovery items' }),
    );

    const persisted = JSON.parse(localStorage.getItem(discoveryInboxStorageKey));
    expect(persisted.every((candidate) => candidate.state === 'Rejected')).toBe(
      true,
    );
  });

  it('reviews discovery items from the mobile review tray', () => {
    const first = addDiscoveryInboxItem({
      evidenceKey: 'manual-search:first',
      searchText: 'first',
      source: 'Search',
      title: 'first',
    });
    const second = addDiscoveryInboxItem({
      evidenceKey: 'manual-search:second',
      searchText: 'second',
      source: 'Search',
      title: 'second',
    });

    render(<DiscoveryInbox />);

    expect(screen.getByText('Mobile Review')).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole('button', { name: 'Select first for mobile review' }),
    );
    expect(screen.getByText('Reviewing: first')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Review next discovery item' }));
    expect(screen.getByText('Reviewing: second')).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole('button', { name: 'Review tray reject second' }),
    );

    let persisted = JSON.parse(localStorage.getItem(discoveryInboxStorageKey));
    expect(persisted.find((candidate) => candidate.id === second.id).state).toBe(
      'Rejected',
    );

    fireEvent.click(
      screen.getByRole('button', { name: 'Select first for mobile review' }),
    );
    fireEvent.click(
      screen.getByRole('button', { name: 'Review tray approve first' }),
    );

    persisted = JSON.parse(localStorage.getItem(discoveryInboxStorageKey));
    expect(persisted.find((candidate) => candidate.id === first.id).state).toBe(
      'Approved',
    );
  });

  it('creates review-only acquisition plans from approved candidates', () => {
    const item = addDiscoveryInboxItem({
      acquisitionProfile: 'mesh-preferred',
      evidenceKey: 'manual-search:rare-track',
      networkImpact: 'Manual review only.',
      reason: 'Saved from Search while using Mesh Preferred.',
      searchText: 'rare track',
      source: 'Search',
      title: 'rare track',
    });

    render(<DiscoveryInbox />);

    fireEvent.click(screen.getByRole('button', { name: 'Approve rare track' }));
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Create acquisition plans for approved discovery items',
      }),
    );

    const persistedPlans = JSON.parse(localStorage.getItem(acquisitionPlanStorageKey));
    expect(persistedPlans).toHaveLength(1);
    expect(persistedPlans[0]).toEqual(
      expect.objectContaining({
        manualOnly: true,
        providerPriority: ['LocalLibrary', 'NativeMesh', 'MeshDht', 'Soulseek'],
        sourceId: item.id,
        state: 'Planned',
      }),
    );
    expect(screen.getByText(/LocalLibrary/)).toBeInTheDocument();

    const persistedInbox = JSON.parse(localStorage.getItem(discoveryInboxStorageKey));
    expect(persistedInbox.find((candidate) => candidate.id === item.id).state).toBe(
      'Staged',
    );
  });

  it('executes ready acquisition plans as bounded search jobs', async () => {
    addDiscoveryInboxItem({
      acquisitionProfile: 'mesh-preferred',
      evidenceKey: 'manual-search:rare-track',
      networkImpact: 'Manual review only.',
      reason: 'Saved from Search while using Mesh Preferred.',
      searchText: 'rare track',
      source: 'Search',
      title: 'rare track',
    });

    render(<DiscoveryInbox />);

    fireEvent.click(screen.getByRole('button', { name: 'Approve rare track' }));
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Create acquisition plans for approved discovery items',
      }),
    );
    fireEvent.click(screen.getByRole('button', { name: 'Execute ready acquisition plans' }));

    expect(await screen.findByText(/Queued 1 acquisition search job/)).toBeInTheDocument();
    expect(createSearch).toHaveBeenCalledWith(
      expect.objectContaining({
        acquisitionProfile: 'mesh-preferred',
        searchText: 'rare track',
      }),
    );
    const persistedPlans = JSON.parse(localStorage.getItem(acquisitionPlanStorageKey));
    expect(persistedPlans[0]).toEqual(
      expect.objectContaining({
        state: 'Queued',
        queuedSearchId: expect.any(String),
      }),
    );
  });

  it('creates Wishlist requests from ready acquisition plans without auto-download', async () => {
    addDiscoveryInboxItem({
      acquisitionProfile: 'mesh-preferred',
      evidenceKey: 'manual-search:rare-track',
      networkImpact: 'Manual review only.',
      reason: 'Saved from Search while using Mesh Preferred.',
      searchText: 'rare track',
      source: 'Search',
      title: 'rare track',
    });

    render(<DiscoveryInbox />);

    fireEvent.click(screen.getByRole('button', { name: 'Approve rare track' }));
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Create acquisition plans for approved discovery items',
      }),
    );
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Create Wishlist requests for ready acquisition plans',
      }),
    );

    expect(await screen.findByText(/Created 1 Wishlist request/)).toBeInTheDocument();
    expect(createWishlist).toHaveBeenCalledWith({
      autoDownload: false,
      enabled: true,
      filter: '',
      maxResults: 50,
      searchText: 'rare track',
    });
    const persistedPlans = JSON.parse(localStorage.getItem(acquisitionPlanStorageKey));
    expect(persistedPlans[0]).toEqual(
      expect.objectContaining({
        state: 'Planned',
        wishlistRequestId: 'wishlist-plan',
      }),
    );
    expect(screen.getByText(/Wishlist request wishlist-plan/)).toBeInTheDocument();
  });

  it('adds watchlist targets and creates review seeds without scanning providers', () => {
    render(<DiscoveryInbox />);

    fireEvent.change(screen.getByLabelText('Watchlist target'), {
      target: { value: 'Stereolab' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Add watchlist target' }));

    const persistedWatchlists = JSON.parse(
      localStorage.getItem(watchlistStorageKey),
    );
    expect(persistedWatchlists).toHaveLength(1);
    expect(persistedWatchlists[0]).toMatchObject({
      acquisitionProfile: 'lossless-exact',
      cooldownDays: 7,
      country: 'Any',
      format: 'Any',
      kind: 'Artist',
      releaseTypes: ['Album', 'EP', 'Single'],
      schedule: 'Manual only',
      target: 'Stereolab',
    });
    expect(
      screen.getAllByText((_content, node) =>
        node.textContent.includes('Album, EP, Single'),
      ).length,
    ).toBeGreaterThan(0);
    expect(screen.getByText('Manual scans only')).toBeInTheDocument();
    expect(screen.getByText('Cooldown 7 days')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Preview scan Stereolab' }));
    expect(
      screen.getByText(/Manual scan preview only; no provider lookup/),
    ).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole('button', { name: 'Send Stereolab to Discovery Inbox' }),
    );

    const persistedInbox = JSON.parse(localStorage.getItem(discoveryInboxStorageKey));
    expect(persistedInbox[0]).toMatchObject({
      evidenceKey: 'watchlist:artist:stereolab',
      source: 'Watchlist',
      title: 'Stereolab',
    });
  });

  it('shows scheduled watchlist enablement and cooldown policy', () => {
    saveWatchlist({
      acquisitionProfile: 'mesh-preferred',
      cooldownDays: 3,
      schedule: 'Weekly',
      target: 'Broadcast',
    });

    render(<DiscoveryInbox />);

    expect(screen.getByText('Weekly schedule visible')).toBeInTheDocument();
    expect(screen.getByText('Cooldown 3 days')).toBeInTheDocument();
    expect(screen.getAllByText('Mesh Preferred').length).toBeGreaterThan(0);
    expect(screen.getByText(/does not execute provider lookups/)).toBeInTheDocument();
  });

  it('approves similar-artist expansion into a manual watchlist', () => {
    saveWatchlist({
      expansionCandidates: ['Broadcast'],
      target: 'Stereolab',
    });

    render(<DiscoveryInbox />);

    expect(screen.getByText('Expansions 1 pending')).toBeInTheDocument();
    expect(screen.getByText(/Similar-artist expansion is review-only/)).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole('button', { name: 'Approve similar artist Broadcast' }),
    );

    const persistedWatchlists = JSON.parse(
      localStorage.getItem(watchlistStorageKey),
    );
    expect(persistedWatchlists[0]).toMatchObject({
      expansionSource: 'Stereolab',
      kind: 'Artist',
      schedule: 'Manual only',
      target: 'Broadcast',
    });
    expect(
      persistedWatchlists
        .find((watchlist) => watchlist.target === 'Stereolab')
        .expansionCandidates,
    ).toContainEqual(
      expect.objectContaining({
        name: 'Broadcast',
        status: 'Approved',
      }),
    );
  });
});
