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

  it('stores optional fingerprint verification for newly staged files', async () => {
    render(<ImportStaging />);

    fireEvent.click(screen.getByRole('checkbox', { name: 'Fingerprint on add' }));
    fireEvent.change(screen.getByTestId('import-staging-file-input'), {
      target: {
        files: [
          new File(['abc'], 'track.flac', {
            lastModified: 123,
            type: 'audio/flac',
          }),
        ],
      },
    });

    expect(await screen.findByText('Verified')).toBeInTheDocument();
    const persisted = JSON.parse(localStorage.getItem(importStagingStorageKey));
    expect(persisted[0].fingerprintVerification).toEqual(
      expect.objectContaining({
        algorithm: 'sha256',
        status: 'Verified',
        value: 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad',
      }),
    );
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

  it('marks table cells with mobile review labels', () => {
    localStorage.setItem(
      importStagingStorageKey,
      JSON.stringify([
        {
          fileName: 'mobile-track.flac',
          id: 'stage-1',
          lastModified: 123,
          size: 3,
          state: 'Staged',
          type: 'audio/flac',
        },
      ]),
    );

    const { container } = render(<ImportStaging />);

    expect(container.querySelector('.import-staging-table')).toBeInTheDocument();
    expect(container.querySelector('td[data-label="File"]')).toBeInTheDocument();
    expect(
      container.querySelector('td[data-label="Metadata Match"]'),
    ).toBeInTheDocument();
    expect(container.querySelector('td[data-label="Actions"]')).toBeInTheDocument();
  });

  it('adds rejected files to the failed-import denylist', () => {
    localStorage.setItem(
      importStagingStorageKey,
      JSON.stringify([
        {
          fileName: 'bad.flac',
          id: 'stage-1',
          lastModified: 123,
          size: 3,
          state: 'Staged',
          type: 'audio/flac',
        },
      ]),
    );

    render(<ImportStaging />);

    fireEvent.click(screen.getByRole('button', { name: 'Reject bad.flac' }));

    expect(screen.getByText('Failed Import Denylist')).toBeInTheDocument();
    expect(screen.getAllByText('bad.flac').length).toBeGreaterThan(0);

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Remove bad.flac from failed import denylist',
      }),
    );

    expect(screen.queryByText('Failed Import Denylist')).not.toBeInTheDocument();
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
