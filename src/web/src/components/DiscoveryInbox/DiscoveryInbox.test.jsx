import DiscoveryInbox from './DiscoveryInbox';
import { acquisitionPlanStorageKey } from '../../lib/acquisitionPlans';
import {
  addDiscoveryInboxItem,
  discoveryInboxStorageKey,
} from '../../lib/discoveryInbox';
import { watchlistStorageKey } from '../../lib/watchlists';
import { fireEvent, render, screen } from '@testing-library/react';
import React from 'react';

describe('DiscoveryInbox', () => {
  beforeEach(() => {
    localStorage.clear();
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
    expect(screen.getByText(/Manual review only/)).toBeInTheDocument();
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
      kind: 'Artist',
      target: 'Stereolab',
    });

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
});
