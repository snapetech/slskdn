import LyricsPane from './LyricsPane';
import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { vi } from 'vitest';

describe('LyricsPane', () => {
  beforeEach(() => {
    Element.prototype.scrollIntoView = vi.fn();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('derives artist and title from local filenames when metadata is placeholder text', async () => {
    window.fetch = vi.fn(() =>
      Promise.resolve({
        json: () =>
          Promise.resolve({
            syncedLyrics: '[00:01.00]First line\n[00:02.00]Second line',
          }),
        ok: true,
      }),
    );

    render(
      <LyricsPane
        audioElement={document.createElement('audio')}
        current={{
          artist: 'slskdN',
          fileName: 'Example Artist - Example Song.ogg',
          title: 'Example Artist - Example Song.ogg',
        }}
        visible
      />,
    );

    await screen.findByText('First line');

    expect(window.fetch).toHaveBeenCalledWith(
      'https://lrclib.net/api/get?artist_name=Example+Artist&track_name=Example+Song',
      expect.any(Object),
    );
  });

  it('falls back to LRCLIB search when exact lyrics lookup misses', async () => {
    window.fetch = vi
      .fn()
      .mockResolvedValueOnce({
        json: () => Promise.resolve(null),
        ok: false,
      })
      .mockResolvedValueOnce({
        json: () =>
          Promise.resolve([
            {
              plainLyrics: 'Plain line one\nPlain line two',
            },
          ]),
        ok: true,
      });

    render(
      <LyricsPane
        audioElement={document.createElement('audio')}
        current={{
          artist: 'Example Artist',
          fileName: 'Example Song.ogg',
          title: 'Example Song',
        }}
        visible
      />,
    );

    await screen.findByText('Plain line one');

    await waitFor(() => {
      expect(window.fetch).toHaveBeenCalledWith(
        'https://lrclib.net/api/search?artist_name=Example+Artist&track_name=Example+Song',
        expect.any(Object),
      );
    });
  });

  it('does not call LRCLIB when artist metadata cannot be inferred', () => {
    window.fetch = vi.fn();

    render(
      <LyricsPane
        audioElement={document.createElement('audio')}
        current={{
          artist: 'slskdN',
          fileName: 'Sample2-public-domain-bansuri.ogg',
          title: 'Sample2-public-domain-bansuri.ogg',
        }}
        visible
      />,
    );

    expect(screen.getByText('Lyrics need artist and title metadata')).toBeInTheDocument();
    expect(window.fetch).not.toHaveBeenCalled();
  });
});
