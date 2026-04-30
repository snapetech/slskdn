import PlayerBar from './PlayerBar';
import React from 'react';
import { PlayerProvider, usePlayer } from './PlayerContext';
import { MemoryRouter } from 'react-router-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { vi } from 'vitest';
import * as externalVisualizer from '../../lib/externalVisualizer';

vi.mock('../../lib/nowPlaying', () => ({
  clearNowPlaying: vi.fn(() => Promise.resolve()),
  setNowPlaying: vi.fn(() => Promise.resolve()),
}));

vi.mock('../../lib/collections', () => ({
  browseLibraryItems: vi.fn(({ path = '', query = '' } = {}) => {
    if (query) {
      return Promise.resolve({
        data: {
          breadcrumbs: [{ name: 'Library', path: '' }],
          directories: [],
          duplicatesRemoved: 2,
          files: [
            {
              bytes: 5242880,
              contentId: 'sha256:library',
              duplicateCount: 3,
              fileName: 'Library stream.ogg',
              mediaKind: 'Audio',
              path: 'Downloads/Library stream.ogg',
            },
          ],
          hasMore: false,
          totalDirectories: 0,
          totalFiles: 1,
        },
      });
    }

    if (path === 'Downloads') {
      return Promise.resolve({
        data: {
          breadcrumbs: [
            { name: 'Library', path: '' },
            { name: 'Downloads', path: 'Downloads' },
          ],
          directories: [],
          duplicatesRemoved: 0,
          files: [
            {
              bytes: 5242880,
              contentId: 'sha256:library',
              duplicateCount: 1,
              fileName: 'Library stream.ogg',
              mediaKind: 'Audio',
              path: 'Downloads/Library stream.ogg',
            },
          ],
          hasMore: false,
          totalDirectories: 0,
          totalFiles: 1,
        },
      });
    }

    return Promise.resolve({
      data: {
        breadcrumbs: [{ name: 'Library', path: '' }],
        directories: [
          {
            childDirectoryCount: 2,
            fileCount: 1,
            name: 'Downloads',
            path: 'Downloads',
          },
        ],
        duplicatesRemoved: 0,
        files: [],
        hasMore: false,
        totalDirectories: 1,
        totalFiles: 0,
      },
    });
  }),
  getCollectionItems: vi.fn(() =>
    Promise.resolve({
      data: [
        {
          contentId: 'sha256:collection',
          fileName: 'Collection stream.ogg',
          id: 'collection-item-1',
          mediaKind: 'Audio',
        },
      ],
    }),
  ),
  getCollections: vi.fn(() =>
    Promise.resolve({ data: [{ id: 'collection-1', title: 'Favorites' }] }),
  ),
  searchLibraryItems: vi.fn(() =>
    Promise.resolve({
      data: {
        items: [
          {
            contentId: 'sha256:library',
            fileName: 'Library stream.ogg',
            mediaKind: 'Audio',
            path: '/downloads/Library stream.ogg',
          },
        ],
      },
    }),
  ),
}));

vi.mock('../../lib/externalVisualizer', () => ({
  getExternalVisualizerStatus: vi.fn(() =>
    Promise.resolve({
      arguments: [],
      available: true,
      configured: true,
      enabled: true,
      name: 'MilkDrop3',
      path: '/opt/MilkDrop3/MilkDrop 3.exe',
      resolvedPath: '/opt/MilkDrop3/MilkDrop 3.exe',
      workingDirectory: '/opt/MilkDrop3',
    }),
  ),
  launchExternalVisualizer: vi.fn(() =>
    Promise.resolve({
      error: null,
      name: 'MilkDrop3',
      processId: 1234,
      started: true,
    }),
  ),
}));

vi.mock('../../lib/streaming', () => ({
  buildDirectStreamUrl: vi.fn((contentId) =>
    `/api/v0/streams/${encodeURIComponent(contentId)}`,
  ),
  buildTicketedStreamUrl: vi.fn((contentId, ticket) =>
    `/api/v0/streams/${encodeURIComponent(contentId)}?ticket=${ticket}`,
  ),
  createStreamTicket: vi.fn(() => Promise.resolve('ticket-1')),
}));

const TestHarness = () => {
  const { playItem } = usePlayer();

  return (
    <>
      <button
        onClick={() =>
          playItem({
            album: 'Fixture Album',
            contentId: 'sha256:test',
            confidence: 0.91,
            fileName: 'Local stream.ogg',
            genre: 'Fixture Genre',
            sourceProviders: ['mesh', 'soulseek'],
            title: 'Local stream',
            verified: true,
          })
        }
        type="button"
      >
        Play fixture
      </button>
      <button
        onClick={() =>
          playItem({
            contentId: 'sha256:second',
            fileName: 'Second stream.ogg',
            title: 'Second stream',
          })
        }
        type="button"
      >
        Play second fixture
      </button>
      <button
        onClick={() =>
          playItem({
            contentId: 'sha256:third',
            fileName: 'Third stream.ogg',
            title: 'Third stream',
          })
        }
        type="button"
      >
        Play third fixture
      </button>
      <PlayerBar />
    </>
  );
};

const renderPlayer = () =>
  render(
    <MemoryRouter>
      <PlayerProvider>
        <TestHarness />
      </PlayerProvider>
    </MemoryRouter>,
  );

describe('PlayerBar', () => {
  beforeEach(() => {
    window.localStorage.clear();
    HTMLMediaElement.prototype.load = vi.fn();
    HTMLMediaElement.prototype.play = vi.fn(() => Promise.resolve());
    HTMLMediaElement.prototype.pause = vi.fn();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('mutes local browser playback without clearing the stream source', async () => {
    renderPlayer();

    fireEvent.click(screen.getByText('Play fixture'));

    const audio = document.querySelector('audio');
    await waitFor(() => {
      const src = audio.getAttribute('src') || '';
      expect(src).toContain(
        '/api/v0/streams/sha256%3Atest',
      );
    });

    fireEvent.click(screen.getByTestId('player-toggle-mute'));

    expect(audio.muted).toBe(true);
    expect(audio.getAttribute('src')).toContain(
      '/api/v0/streams/sha256%3Atest',
    );
    expect(window.localStorage.getItem('slskdn.player.localMuted')).toBe('true');
  });

  it('restores the local mute preference for the PWA/browser session', () => {
    window.localStorage.setItem('slskdn.player.localMuted', 'true');

    renderPlayer();
    fireEvent.click(screen.getByText('Play fixture'));

    expect(document.querySelector('audio').muted).toBe(true);
  });

  it('opens collection and local file browser modals before playback starts', async () => {
    renderPlayer();

    expect(
      screen.getByTestId('player-open-collections-browser'),
    ).toBeInTheDocument();
    expect(screen.getByTestId('player-open-file-browser')).toBeInTheDocument();
    await screen.findByText('Pick a collection or local audio file');

    fireEvent.click(screen.getByTestId('player-open-collections-browser'));
    expect(
      screen.getByTestId('player-collection-browser-modal'),
    ).toBeInTheDocument();
    fireEvent.click(await screen.findByTestId('player-collection-row-collection-1'));
    expect(await screen.findByText('Collection stream.ogg')).toBeInTheDocument();

    fireEvent.click(screen.getAllByText('Close')[0]);
    fireEvent.click(screen.getByTestId('player-open-file-browser'));
    expect(screen.getByTestId('player-file-browser-modal')).toBeInTheDocument();
    expect(await screen.findByText('Downloads')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('player-file-folder-Downloads'));
    expect(await screen.findByText('Library stream.ogg')).toBeInTheDocument();
  });

  it('searches the local file browser as a deduplicated explorer', async () => {
    renderPlayer();

    fireEvent.click(screen.getByTestId('player-open-file-browser'));
    fireEvent.change(screen.getByTestId('player-file-browser-search').querySelector('input'), {
      target: { value: 'library' },
    });

    expect(await screen.findByText('Library stream.ogg')).toBeInTheDocument();
    expect(screen.getByText(/2 duplicates collapsed/u)).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('switches the visual tile from album art to the MilkDrop canvas', () => {
    renderPlayer();

    expect(screen.getByTestId('player-album-art')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('player-visual-tile'));

    expect(document.querySelector('.player-visualizer-canvas')).toBeInTheDocument();
    expect(screen.queryByTestId('player-album-art')).not.toBeInTheDocument();
  });

  it('does not repeat the currently playing track in the queue preview', () => {
    renderPlayer();

    fireEvent.click(screen.getByText('Play fixture'));

    expect(screen.getByText('Local stream')).toBeInTheDocument();
    expect(document.querySelector('.player-queue')).not.toBeInTheDocument();

    fireEvent.click(screen.getByText('Play second fixture'));

    expect(screen.getByText('Second stream')).toBeInTheDocument();
    expect(screen.getByText('Local stream')).toBeInTheDocument();
    expect(document.querySelector('.player-queue')?.textContent).not.toContain(
      'Second stream',
    );
  });

  it('makes ListenBrainz autosave explicit and clearable', () => {
    renderPlayer();

    fireEvent.click(screen.getByTestId('player-open-integrations'));
    const tokenInput = screen.getByLabelText('ListenBrainz user token');

    fireEvent.change(tokenInput, { target: { value: ' token-1 ' } });

    expect(screen.getByTestId('player-listenbrainz-save-state')).toHaveTextContent(
      'saved automatically',
    );
    expect(window.localStorage.getItem('slskdn.listenbrainz.token')).toBe('token-1');
    expect(screen.getByTestId('player-close-integrations')).toHaveTextContent('Done');

    fireEvent.click(screen.getByTestId('player-clear-listenbrainz-token'));

    expect(tokenInput).toHaveValue('');
    expect(window.localStorage.getItem('slskdn.listenbrainz.token')).toBeNull();
  });

  it('shows and launches the configured external visualizer', async () => {
    renderPlayer();

    fireEvent.click(screen.getByTestId('player-open-integrations'));

    expect(
      await screen.findByText('Ready to launch on the slskdN host.'),
    ).toBeInTheDocument();
    expect(screen.getByText('/opt/MilkDrop3/MilkDrop 3.exe')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('player-launch-external-visualizer'));

    await waitFor(() => {
      expect(externalVisualizer.launchExternalVisualizer).toHaveBeenCalled();
    });
    expect(await screen.findByText('MilkDrop3 launched.')).toBeInTheDocument();
  });

  it('shows now-playing source badges and stores local discovery ratings', async () => {
    renderPlayer();

    fireEvent.click(screen.getByText('Play fixture'));

    expect(await screen.findByTestId('player-badge-source-Mesh')).toHaveTextContent(
      'Mesh',
    );
    expect(screen.getByTestId('player-badge-source-Soulseek')).toHaveTextContent(
      'Soulseek',
    );
    expect(screen.getByTestId('player-badge-confidence')).toHaveTextContent(
      '91% match',
    );
    expect(screen.getByTestId('player-badge-verified')).toHaveTextContent(
      'Verified',
    );

    fireEvent.click(screen.getByTestId('player-rating-5'));

    expect(screen.getByTestId('player-rating-controls')).toHaveTextContent(
      'Discovery boost',
    );
    expect(window.localStorage.getItem('slskdn.player.ratings')).toContain(
      '"content:sha256:test":5',
    );
    expect(window.localStorage.getItem('slskdn.discovery.shelf')).toContain(
      '"action":"promote-preview"',
    );

    fireEvent.click(screen.getByTestId('player-open-discovery-shelf'));

    expect(await screen.findByText('Discovery Shelf')).toBeInTheDocument();
    expect(screen.getByTestId('player-shelf-summary')).toHaveTextContent(
      '1local review items',
    );
    expect(screen.getByTestId('player-shelf-row-content:sha256:test')).toHaveTextContent(
      'Promote preview',
    );
    expect(screen.getByTestId('player-shelf-policy-preview')).toHaveTextContent(
      '1 promote',
    );
    expect(screen.getByTestId('player-shelf-policy-preview')).toHaveTextContent(
      '0 consensus gated',
    );
    fireEvent.click(screen.getByTestId('player-shelf-copy-policy-report'));
    expect(screen.getByText('Policy report prepared for 1 shelf items.')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('player-shelf-preview-content:sha256:test'));
    expect(screen.getByText('Promote preview prepared for Local stream. No files were moved or deleted.')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('player-close-discovery-shelf'));

    fireEvent.click(screen.getByTestId('player-rating-5'));

    expect(screen.getByTestId('player-rating-controls')).toHaveTextContent(
      'Not rated',
    );
  });

  it('handles player keyboard shortcuts without stealing input typing', async () => {
    renderPlayer();

    fireEvent.click(screen.getByText('Play fixture'));
    const audio = document.querySelector('audio');
    await waitFor(() => {
      expect(audio.getAttribute('src')).toContain('/api/v0/streams/sha256%3Atest');
    });

    fireEvent.keyDown(window, { key: 'm' });
    expect(audio.muted).toBe(true);

    fireEvent.keyDown(window, { key: 'e' });
    expect(document.querySelector('.player-panel-eq')).toBeInTheDocument();

    fireEvent.keyDown(window, { key: 'v' });
    expect(document.querySelector('.player-visualizer-canvas')).toBeInTheDocument();

    fireEvent.keyDown(window, { key: 'ArrowRight' });
    expect(audio.currentTime).toBe(30);

    fireEvent.click(screen.getByTestId('player-open-integrations'));
    const tokenInput = screen.getByLabelText('ListenBrainz user token');
    fireEvent.keyDown(tokenInput, { key: 'm' });

    expect(audio.muted).toBe(true);
  });

  it('opens smart radio seeds without starting a search automatically', async () => {
    renderPlayer();

    fireEvent.click(screen.getByText('Play fixture'));
    fireEvent.click(screen.getByTestId('player-open-radio'));

    expect(await screen.findByText('Smart Radio Seed')).toBeInTheDocument();
    expect(screen.getByTestId('player-radio-seed')).toHaveTextContent(
      'slskdN - Local stream',
    );
    expect(screen.getByText('Similar track seed')).toBeInTheDocument();
    expect(screen.getByText('slskdN Local stream')).toBeInTheDocument();
    expect(screen.getByText('Album neighborhood')).toBeInTheDocument();
    expect(screen.getByText('slskdN Fixture Album')).toBeInTheDocument();
    expect(screen.getByText('Artist and genre seed')).toBeInTheDocument();
    expect(screen.getByText('slskdN Fixture Genre')).toBeInTheDocument();
  });

  it('manages the playback queue without removing the current track', async () => {
    renderPlayer();

    fireEvent.click(screen.getByText('Play fixture'));
    fireEvent.click(screen.getByText('Play second fixture'));
    fireEvent.click(screen.getByText('Play third fixture'));
    fireEvent.click(screen.getByTestId('player-open-queue'));

    expect(await screen.findByText('Playback Queue')).toBeInTheDocument();
    expect(screen.getByText('Now Playing')).toBeInTheDocument();
    expect(screen.getAllByText('Third stream')).toHaveLength(2);
    expect(screen.getByTestId('player-queue-row-sha256:second')).toHaveTextContent(
      'Second stream',
    );
    expect(screen.getByTestId('player-queue-row-sha256:test')).toHaveTextContent(
      'Local stream',
    );
    expect(screen.getByText('Recent')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('player-remove-queue-sha256:second'));

    expect(screen.queryByTestId('player-queue-row-sha256:second')).not.toBeInTheDocument();
    expect(screen.getAllByText('Third stream')).toHaveLength(2);

    fireEvent.click(screen.getByTestId('player-clear-upcoming'));

    expect(screen.getByText('No upcoming tracks.')).toBeInTheDocument();
    expect(screen.getAllByText('Third stream')).toHaveLength(2);

    fireEvent.click(screen.getByTestId('player-auto-queue-similar'));

    expect(screen.getByTestId('player-queue-row-sha256:second')).toHaveTextContent(
      'Second stream',
    );
    expect(screen.getByTestId('player-queue-row-sha256:test')).toHaveTextContent(
      'Local stream',
    );
  });

  it('records local listening history and shows browser stats', async () => {
    renderPlayer();

    fireEvent.click(screen.getByText('Play fixture'));
    const audio = document.querySelector('audio');

    Object.defineProperty(audio, 'duration', {
      configurable: true,
      value: 120,
    });
    Object.defineProperty(audio, 'currentTime', {
      configurable: true,
      value: 61,
      writable: true,
    });
    fireEvent.timeUpdate(audio);

    fireEvent.click(screen.getByTestId('player-open-listening-stats'));

    expect(await screen.findByText('Listening Stats')).toBeInTheDocument();
    expect(screen.getByTestId('player-stats-summary')).toHaveTextContent(
      '1local plays recorded',
    );
    expect(screen.getByText('Top Artists')).toBeInTheDocument();
    expect(screen.getByText('Top Genres')).toBeInTheDocument();
    expect(screen.getByText('Recommendation Seeds')).toBeInTheDocument();
    expect(screen.getAllByText('slskdN').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Fixture Genre').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Local stream').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByTestId('player-stats-search-seed-Fixture Genre')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('player-clear-listening-history'));

    expect(screen.getByTestId('player-stats-summary')).toHaveTextContent(
      '0local plays recorded',
    );
  });

  it('imports pasted media-server listening history into browser stats', async () => {
    renderPlayer();

    fireEvent.click(screen.getByText('Play fixture'));
    fireEvent.click(screen.getByTestId('player-open-listening-stats'));

    expect(await screen.findByText('Listening Stats')).toBeInTheDocument();

    fireEvent.change(screen.getByTestId('player-listening-history-import-text'), {
      target: {
        value: [
          'playedAt,artist,album,title,genre',
          '2026-04-30T20:00:00Z,Imported Artist,Imported Album,Imported Track,Imported Genre',
        ].join('\n'),
      },
    });
    fireEvent.click(screen.getByTestId('player-listening-history-import'));

    expect(screen.getByText('1 imported, 0 skipped as duplicates or incomplete rows.')).toBeInTheDocument();
    expect(screen.getByTestId('player-stats-summary')).toHaveTextContent(
      '1local plays recorded',
    );
    expect(screen.getAllByText('Imported Artist').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Imported Genre').length).toBeGreaterThanOrEqual(1);
  });
});
