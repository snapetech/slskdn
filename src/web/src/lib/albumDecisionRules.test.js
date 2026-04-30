import {
  buildAlbumDecisionRule,
  getAlbumDecisionRules,
  saveAlbumDecisionRule,
} from './albumDecisionRules';

describe('albumDecisionRules', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('builds a deterministic browser-local album decision rule preview', () => {
    const rule = buildAlbumDecisionRule({
      candidate: {
        albumTitle: 'Album Deluxe',
        completenessRatio: 0.9,
        expectedTrackCount: 10,
        formatMix: [
          { count: 8, format: 'FLAC' },
          { count: 2, format: 'MP3' },
        ],
        sourceCount: 2,
        substitutionOptions: [
          { optionCount: 2, trackNumber: 3 },
        ],
        warnings: ['mixed audio formats'],
      },
      createdAt: '2026-04-30T20:34:08.000Z',
      searchText: 'Artist Album Deluxe',
    });

    expect(rule).toMatchObject({
      albumKey: 'album deluxe',
      expectedTrackCount: 10,
      formatPolicy: 'FLAC:8,MP3:2',
      id: 'album deluxe:10:FLAC:8,MP3:2',
      notes: [
        'warn:mixed audio formats',
        'substitute:track-3:2-options',
      ],
      searchKey: 'artist album deluxe',
      substitutionTracks: [3],
      warningCount: 1,
    });
  });

  it('saves and replaces matching rules without growing duplicates', () => {
    const candidate = {
      albumTitle: 'Album Deluxe',
      expectedTrackCount: 4,
      formatMix: [{ count: 4, format: 'FLAC' }],
    };

    saveAlbumDecisionRule({ candidate, searchText: 'artist album deluxe' });
    saveAlbumDecisionRule({ candidate, searchText: 'artist album deluxe' });

    expect(getAlbumDecisionRules()).toHaveLength(1);
  });
});
