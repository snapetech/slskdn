import DiscoveryInbox from './DiscoveryInbox';
import {
  addDiscoveryInboxItem,
  discoveryInboxStorageKey,
} from '../../lib/discoveryInbox';
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

    fireEvent.click(screen.getByRole('button', { name: 'Approve rare track' }));

    const persisted = JSON.parse(localStorage.getItem(discoveryInboxStorageKey));
    expect(persisted.find((candidate) => candidate.id === item.id).state).toBe(
      'Approved',
    );
    expect(screen.getAllByText('Approved').length).toBeGreaterThan(0);
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
});
