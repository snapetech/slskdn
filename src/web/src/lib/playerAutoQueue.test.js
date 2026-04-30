import { buildSimilarQueueCandidates } from './playerAutoQueue';

describe('playerAutoQueue', () => {
  it('finds similar recent tracks that are not already queued', () => {
    const candidates = buildSimilarQueueCandidates({
      current: {
        album: 'Fixture Album',
        artist: 'Fixture Artist',
        contentId: 'sha256:current',
        genre: 'Fixture Genre',
        title: 'Current Song',
      },
      history: [
        {
          album: 'Fixture Album',
          artist: 'Fixture Artist',
          contentId: 'sha256:album-match',
          title: 'Album Match',
        },
        {
          artist: 'Other Artist',
          contentId: 'sha256:tag-match',
          tags: ['Fixture Genre'],
          title: 'Tag Match',
        },
        {
          artist: 'Other Artist',
          contentId: 'sha256:miss',
          title: 'Miss',
        },
      ],
      queue: [{ contentId: 'sha256:current' }],
    });

    expect(candidates.map((candidate) => candidate.item.contentId)).toEqual([
      'sha256:album-match',
      'sha256:tag-match',
    ]);
  });

  it('skips duplicate and already queued candidates', () => {
    const candidates = buildSimilarQueueCandidates({
      current: {
        artist: 'Fixture Artist',
        contentId: 'sha256:current',
        title: 'Current Song',
      },
      history: [
        {
          artist: 'Fixture Artist',
          contentId: 'sha256:queued',
          title: 'Already Queued',
        },
        {
          artist: 'Fixture Artist',
          contentId: 'sha256:new',
          title: 'New Candidate',
        },
        {
          artist: 'Fixture Artist',
          contentId: 'sha256:new',
          title: 'Duplicate Candidate',
        },
      ],
      queue: [
        { contentId: 'sha256:current' },
        { contentId: 'sha256:queued' },
      ],
    });

    expect(candidates.map((candidate) => candidate.item.contentId)).toEqual([
      'sha256:new',
    ]);
  });

  it('returns no candidates without a current track', () => {
    expect(buildSimilarQueueCandidates({ current: null })).toEqual([]);
  });
});
