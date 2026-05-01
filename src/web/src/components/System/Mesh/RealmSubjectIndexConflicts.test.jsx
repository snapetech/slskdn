// <copyright file="RealmSubjectIndexConflicts.test.jsx" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import RealmSubjectIndexConflicts from './RealmSubjectIndexConflicts';
import { fetchRealmSubjectIndexConflicts } from '../../../lib/realmSubjectIndexes';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';
import { vi } from 'vitest';

vi.mock('../../../lib/realmSubjectIndexes', async () => {
  const actual = await vi.importActual('../../../lib/realmSubjectIndexes');
  return {
    ...actual,
    fetchRealmSubjectIndexConflicts: vi.fn(),
  };
});

describe('RealmSubjectIndexConflicts', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    Object.assign(navigator, {
      clipboard: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
    });
    fetchRealmSubjectIndexConflicts.mockResolvedValue({
      data: {
        conflicts: [
          {
            description: 'Recording maps to conflicting MusicBrainz IDs.',
            id: 'conflict-1',
            key: 'musicbrainz:recording',
            subjectId: 'subject-1',
            subjectNamespace: 'recording',
            type: 'external-id',
            values: [
              {
                authorityKey: 'scene-realm:index-a:r1',
                provenance: 'realm:scene-realm:subject-index:index-a:r1',
                value: 'mbid-a',
              },
              {
                authorityKey: 'scene-realm:index-b:r2',
                provenance: 'realm:scene-realm:subject-index:index-b:r2',
                value: 'mbid-b',
              },
            ],
          },
        ],
        entryCount: 7,
        indexCount: 2,
        realmId: 'scene-realm',
      },
    });
  });

  it('loads realm subject-index conflicts with provenance and local authority disables', async () => {
    render(<RealmSubjectIndexConflicts />);

    fireEvent.click(
      screen.getByRole('button', { name: 'Load realm subject-index conflicts' }),
    );

    expect(await screen.findByText('musicbrainz:recording')).toBeInTheDocument();
    expect(screen.getByText('mbid-a')).toBeInTheDocument();
    expect(
      screen.getByText('realm:scene-realm:subject-index:index-b:r2'),
    ).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Disable authority scene-realm:index-b:r2',
      }),
    );

    expect(screen.getByText('Locally disabled')).toBeInTheDocument();
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Copy realm subject-index conflict report',
      }),
    );

    await waitFor(() => {
      expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
        expect.stringContaining('DISABLED scene-realm:index-b:r2: mbid-b'),
      );
    });
  });
});
