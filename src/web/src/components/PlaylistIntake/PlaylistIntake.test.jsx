import PlaylistIntake from './PlaylistIntake';
import * as collectionsAPI from '../../lib/collections';
import { discoveryInboxStorageKey } from '../../lib/discoveryInbox';
import { playlistIntakeStorageKey } from '../../lib/playlistIntake';
import * as sourceFeedImportsAPI from '../../lib/sourceFeedImports';
import { fireEvent, render, screen } from '@testing-library/react';
import React from 'react';
import { vi } from 'vitest';

vi.mock('../../lib/collections', () => ({
  addCollectionItem: vi.fn(),
  createCollection: vi.fn(),
}));

vi.mock('../../lib/sourceFeedImports', () => ({
  previewSourceFeedImport: vi.fn(),
}));

describe('PlaylistIntake', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  it('imports playlist rows locally and sends rows to Discovery Inbox review', () => {
    render(<PlaylistIntake />);

    fireEvent.change(screen.getByLabelText('Playlist name'), {
      target: { value: 'Road trip' },
    });
    fireEvent.change(screen.getByLabelText('Playlist source'), {
      target: { value: 'local:road-trip.csv' },
    });
    fireEvent.click(screen.getByLabelText('Mirror playlist for refresh review'));
    fireEvent.change(screen.getByLabelText('Playlist rows'), {
      target: {
        value: 'Stereolab,French Disko\nBroadcast - Come On Let\'s Go',
      },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Import playlist for review' }));

    const persisted = JSON.parse(localStorage.getItem(playlistIntakeStorageKey));
    expect(persisted).toHaveLength(1);
    expect(persisted[0]).toMatchObject({
      mirrorEnabled: true,
      name: 'Road trip',
      provider: 'CSV',
      refreshAutomationEnabled: false,
      refreshCadence: 'Manual review',
      source: 'local:road-trip.csv',
    });
    expect(persisted[0].tracks).toHaveLength(2);
    expect(
      screen.getAllByText((_content, node) =>
        node.textContent.includes('Mirror review enabled'),
      ).length,
    ).toBeGreaterThan(0);
    expect(screen.getByText('Tracks')).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole('button', { name: 'Send French Disko to Discovery Inbox' }),
    );

    const inbox = JSON.parse(localStorage.getItem(discoveryInboxStorageKey));
    expect(inbox[0]).toMatchObject({
      source: 'Playlist Intake',
      title: 'Stereolab - French Disko',
    });
    expect(inbox[0].networkImpact).toMatch(/no provider fetch/i);
  });

  it('previews mirror refresh diffs and supports row review states', () => {
    render(<PlaylistIntake />);

    fireEvent.change(screen.getByLabelText('Playlist name'), {
      target: { value: 'Mirror queue' },
    });
    fireEvent.change(screen.getByLabelText('Playlist source'), {
      target: { value: 'local:mirror.m3u' },
    });
    fireEvent.click(screen.getByLabelText('Mirror playlist for refresh review'));
    fireEvent.change(screen.getByLabelText('Playlist rows'), {
      target: {
        value: 'Stereolab - French Disko\nBroadcast - Come On Let\'s Go',
      },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Import playlist for review' }));

    expect(screen.getByText('Ready for review')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Mark French Disko unmatched' }));
    expect(screen.getByText('Partial completion allowed')).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole('button', { name: 'Reject playlist row Come On Let\'s Go' }),
    );
    expect(screen.getByText('Rejected 1')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Refresh rows for Mirror queue'), {
      target: {
        value: 'Broadcast - Come On Let\'s Go\nPram - Track of the Cat',
      },
    });
    fireEvent.click(
      screen.getByRole('button', { name: 'Preview refresh for Mirror queue' }),
    );

    expect(screen.getByText('Added')).toBeInTheDocument();
    expect(screen.getByText('Removed')).toBeInTheDocument();
    expect(
      screen.getByText(
        /Refresh diff preview only: 1 added, 1 removed, 2 changed, 1 unchanged/,
      ),
    ).toBeInTheDocument();
    expect(screen.getByText('Changed')).toBeInTheDocument();
    expect(screen.getByText('Automation disabled')).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Send reviewable rows from Mirror queue to Discovery Inbox',
      }),
    );
    expect(screen.getByText('Sent')).toBeInTheDocument();

    const inbox = JSON.parse(localStorage.getItem(discoveryInboxStorageKey));
    expect(inbox).toHaveLength(1);
    expect(inbox[0]).toMatchObject({
      source: 'Playlist Intake',
      title: 'Stereolab - French Disko',
    });

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Preview slskdN playlist for Mirror queue',
      }),
    );
    expect(
      screen.getByText(/Playlist build preview only; creating it writes/i),
    ).toBeInTheDocument();
    expect(screen.getByText(/# Mirror queue/)).toBeInTheDocument();
  });

  it('fetches provider refreshes, enables scheduling, and creates playlist collections', async () => {
    sourceFeedImportsAPI.previewSourceFeedImport.mockResolvedValue({
      networkRequestCount: 1,
      provider: 'Spotify',
      suggestionCount: 2,
      suggestions: [
        {
          artist: 'Stereolab',
          searchText: 'Stereolab French Disko',
          title: 'French Disko',
        },
        {
          artist: 'Pram',
          searchText: 'Pram Track of the Cat',
          title: 'Track of the Cat',
        },
      ],
    });
    collectionsAPI.createCollection.mockResolvedValue({
      data: { id: 'collection-1' },
    });
    collectionsAPI.addCollectionItem.mockResolvedValue({});

    render(<PlaylistIntake />);

    fireEvent.change(screen.getByLabelText('Playlist name'), {
      target: { value: 'Provider mirror' },
    });
    fireEvent.change(screen.getByLabelText('Playlist source'), {
      target: { value: 'https://open.spotify.com/playlist/test' },
    });
    fireEvent.click(screen.getByLabelText('Mirror playlist for refresh review'));
    fireEvent.change(screen.getByLabelText('Playlist rows'), {
      target: { value: 'Stereolab - French Disko' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Import playlist for review' }));

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Enable scheduled refresh for Provider mirror',
      }),
    );
    expect(screen.getByText(/Automation enabled/)).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Fetch provider refresh for Provider mirror',
      }),
    );

    expect(
      await screen.findByText(/Previewed 2 provider rows for Provider mirror/),
    ).toBeInTheDocument();
    expect(sourceFeedImportsAPI.previewSourceFeedImport).toHaveBeenCalledWith(
      expect.objectContaining({
        fetchProviderUrls: true,
        sourceText: 'https://open.spotify.com/playlist/test',
      }),
    );

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Create slskdN playlist for Provider mirror',
      }),
    );

    expect(
      await screen.findByText(/Created playlist collection for Provider mirror/),
    ).toBeInTheDocument();
    expect(collectionsAPI.createCollection).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'Provider mirror',
        type: 'Playlist',
      }),
    );
    expect(collectionsAPI.addCollectionItem).toHaveBeenCalledWith(
      'collection-1',
      expect.objectContaining({
        mediaKind: 'PlannedTrack',
      }),
    );
  });

  it('previews tag organization plans without writing files', () => {
    render(<PlaylistIntake />);

    fireEvent.change(screen.getByLabelText('Playlist name'), {
      target: { value: 'Organization queue' },
    });
    fireEvent.change(screen.getByLabelText('Playlist source'), {
      target: { value: 'local:organization.csv' },
    });
    fireEvent.change(screen.getByLabelText('Playlist rows'), {
      target: {
        value: 'Stereolab,French Disko\nUntitled',
      },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Import playlist for review' }));

    fireEvent.change(
      screen.getByLabelText('Organization album title for Organization queue'),
      {
        target: { value: 'Road Trip Tags' },
      },
    );
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Preview tag organization for Organization queue',
      }),
    );

    expect(
      screen.getByText(/Prepared tag and organization dry run for Organization queue/),
    ).toBeInTheDocument();
    expect(screen.getByText(/no tag write, cover-art write/i)).toBeInTheDocument();
    expect(
      screen.getByText('Stereolab/Road Trip Tags/01 - French Disko.flac'),
    ).toBeInTheDocument();
    expect(screen.getByText('Changed fields')).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Copy tag organization report for Organization queue',
      }),
    );
    expect(
      screen.getByText(/Prepared tag and organization report for Organization queue/),
    ).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Approve tag organization snapshot for Organization queue',
      }),
    );
    expect(
      screen.getByText(/Approved tag and organization snapshot for Organization queue/),
    ).toBeInTheDocument();
    expect(screen.getByText('Snapshot approved')).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Clear tag organization snapshot for Organization queue',
      }),
    );
    expect(
      screen.getByText(/Cleared tag and organization snapshot for Organization queue/),
    ).toBeInTheDocument();

    const persisted = JSON.parse(localStorage.getItem(playlistIntakeStorageKey));
    expect(persisted[0].organizationPlan.summary).toMatchObject({
      matched: 1,
      skipped: 1,
    });
    expect(persisted[0].organizationApproval).toBeNull();
    expect(persisted[0].tracks.map((track) => track.state)).toEqual([
      'Matched',
      'Unmatched',
    ]);
  });
});
