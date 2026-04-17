// <copyright file="SearchListRow.test.jsx" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import SearchListRow from './SearchListRow';
import { MemoryRouter } from 'react-router-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';

vi.mock('../SearchStatusIcon', () => ({
  default: () => <span data-testid="search-status-icon" />,
}));
vi.mock('./SearchActionIcon', () => ({
  default: () => <span data-testid="search-action-icon" />,
}));
vi.mock('../DiscoveryGraphModal', () => ({
  default: () => null,
}));
vi.mock('../../../lib/discoveryGraph', () => ({
  buildDiscoveryGraph: vi.fn(),
}));
vi.mock('../../../lib/searches', () => ({
  createBatch: vi.fn(),
}));

describe('SearchListRow', () => {
  it('links to the existing search detail route instead of replaying the query', () => {
    render(
      <MemoryRouter initialEntries={['/searches']}>
        <table>
          <tbody>
            <SearchListRow
              onRemove={() => {}}
              onStop={() => {}}
              search={{
                endedAt: '2026-04-09T12:00:00Z',
                fileCount: 12,
                id: 'search-123',
                lockedFileCount: 0,
                responseCount: 3,
                searchText: 'metallica one',
                state: 'Complete',
              }}
            />
          </tbody>
        </table>
      </MemoryRouter>,
    );

    expect(screen.getByRole('link', { name: 'metallica one' })).toHaveAttribute(
      'href',
      '/searches/search-123',
    );
  });
});
