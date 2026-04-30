import ImportStaging from './ImportStaging';
import { importStagingStorageKey } from '../../lib/importStaging';
import { fireEvent, render, screen } from '@testing-library/react';
import React from 'react';

describe('ImportStaging', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('adds selected files to staging review', () => {
    render(<ImportStaging />);

    const input = screen.getByTestId('import-staging-file-input');
    fireEvent.change(input, {
      target: {
        files: [
          new File(['abc'], 'track.flac', {
            lastModified: 123,
            type: 'audio/flac',
          }),
        ],
      },
    });

    expect(screen.getByText('track.flac')).toBeInTheDocument();
    const persisted = JSON.parse(localStorage.getItem(importStagingStorageKey));
    expect(persisted).toHaveLength(1);
  });

  it('persists review state changes', () => {
    localStorage.setItem(
      importStagingStorageKey,
      JSON.stringify([
        {
          fileName: 'track.flac',
          id: 'stage-1',
          lastModified: 123,
          size: 3,
          state: 'Staged',
          type: 'audio/flac',
        },
      ]),
    );

    render(<ImportStaging />);

    fireEvent.click(screen.getByRole('button', { name: 'Mark track.flac ready' }));

    const persisted = JSON.parse(localStorage.getItem(importStagingStorageKey));
    expect(persisted[0].state).toBe('Ready');
    expect(screen.getAllByText('Ready').length).toBeGreaterThan(0);
  });

  it('shows local metadata match confidence', () => {
    localStorage.setItem(
      importStagingStorageKey,
      JSON.stringify([
        {
          fileName: 'Artist - Album - 01 - Track.flac',
          id: 'stage-1',
          lastModified: 123,
          size: 3,
          state: 'Staged',
          type: 'audio/flac',
        },
      ]),
    );

    render(<ImportStaging />);

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Match metadata for Artist - Album - 01 - Track.flac',
      }),
    );

    expect(screen.getByText('Strong Match')).toBeInTheDocument();
    expect(screen.getByText('Artist - Album - Track')).toBeInTheDocument();
    const persisted = JSON.parse(localStorage.getItem(importStagingStorageKey));
    expect(persisted[0].metadataMatch).toEqual(
      expect.objectContaining({
        artist: 'Artist',
        status: 'Strong Match',
        title: 'Track',
      }),
    );
  });
});
