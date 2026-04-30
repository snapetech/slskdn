import {
  buildAlbumCandidates,
  getAlbumCandidateFilter,
} from './albumCandidatePicker';

describe('buildAlbumCandidates', () => {
  it('groups album-shaped result folders across multiple sources', () => {
    const candidates = buildAlbumCandidates({
      responses: [
        {
          candidateRank: { score: 84 },
          files: [
            { filename: 'Artist/Album Deluxe/01 First.flac', size: 20_000_000 },
            { filename: 'Artist/Album Deluxe/02 Second.flac', size: 21_000_000 },
            { filename: 'Artist/Album Deluxe/03 Third.flac', size: 19_000_000 },
          ],
          username: 'peer-a',
        },
        {
          candidateRank: { score: 71 },
          files: [
            { filename: 'Music/Album Deluxe/03 Third.mp3', size: 7_500_000 },
            { filename: 'Music/Album Deluxe/04 Fourth.mp3', size: 8_000_000 },
            { filename: 'Music/Album Deluxe/cover.jpg', size: 500_000 },
          ],
          username: 'peer-b',
        },
      ],
      searchText: 'album deluxe',
    });

    expect(candidates).toHaveLength(1);
    expect(candidates[0]).toMatchObject({
      albumTitle: 'Album Deluxe',
      formatMix: [
        { count: 3, format: 'FLAC' },
        { count: 2, format: 'MP3' },
      ],
      losslessCount: 3,
      sourceCount: 2,
      sources: ['peer-a', 'peer-b'],
      substitutionOptions: [
        expect.objectContaining({
          formats: ['FLAC', 'MP3'],
          optionCount: 2,
          sources: ['peer-a', 'peer-b'],
          trackNumber: 3,
        }),
      ],
      trackCount: 5,
      trackNumbers: [1, 2, 3, 4],
      warnings: ['mixed audio formats', 'mixed lossless/lossy evidence'],
    });
    expect(candidates[0].score).toBeGreaterThan(75);
    expect(candidates[0].reasons).toEqual(
      expect.arrayContaining([
        '3 lossless files',
        '2 sources',
        'numbered track run',
      ]),
    );
  });

  it('surfaces local confidence warnings for incomplete album folders', () => {
    const candidates = buildAlbumCandidates({
      responses: [
        {
          files: [
            { filename: 'Artist/Album/01 First.mp3', length: 180 },
            { filename: 'Artist/Album/03 Third.mp3', length: 185 },
            { filename: 'Artist/Album/04 Fourth.mp3', length: 1_300 },
          ],
          username: 'peer-a',
        },
      ],
    });

    expect(candidates).toHaveLength(1);
    expect(candidates[0].missingTrackNumbers).toEqual([2]);
    expect(candidates[0].warnings).toEqual(
      expect.arrayContaining([
        'large duration variance',
        'missing tracks 2',
        'single source only',
      ]),
    );
  });

  it('ignores folders with too few audio files', () => {
    const candidates = buildAlbumCandidates({
      responses: [
        {
          files: [
            { filename: 'Artist/Single/01 Track.flac' },
            { filename: 'Artist/Single/cover.jpg' },
          ],
          username: 'peer-a',
        },
      ],
    });

    expect(candidates).toEqual([]);
  });
});

describe('getAlbumCandidateFilter', () => {
  it('builds a compact search filter from the album title', () => {
    expect(
      getAlbumCandidateFilter({
        albumTitle: 'Selected Ambient Works 85-92 (Remastered)',
      }),
    ).toBe('selected ambient works');
  });
});
