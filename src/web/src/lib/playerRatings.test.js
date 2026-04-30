import {
  getPlayerRating,
  getPlayerRatingKey,
  getPlayerRatingSummary,
  playerRatingsStorageKey,
  setPlayerRating,
} from './playerRatings';

describe('playerRatings', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('stores ratings by stable content id', () => {
    const track = {
      artist: 'Fixture Artist',
      contentId: 'sha256:fixture-track',
      title: 'Fixture Track',
    };

    expect(getPlayerRatingKey(track)).toBe('content:sha256:fixture-track');
    expect(setPlayerRating(track, 5)).toBe(5);
    expect(getPlayerRating(track)).toBe(5);
    expect(JSON.parse(window.localStorage.getItem(playerRatingsStorageKey))).toEqual({
      'content:sha256:fixture-track': 5,
    });
  });

  it('falls back to metadata when no content id exists', () => {
    const track = {
      album: 'Fixture Album',
      artist: 'Fixture Artist',
      title: 'Fixture Track',
    };

    expect(getPlayerRatingKey(track)).toBe(
      'meta:fixture artist|fixture album|fixture track',
    );
    setPlayerRating(track, 2);

    expect(getPlayerRatingSummary(track)).toEqual({
      label: 'Discovery caution',
      rating: 2,
      tone: 'negative',
    });
  });

  it('clears invalid or repeated ratings', () => {
    const track = {
      contentId: 'sha256:clear-rating',
      title: 'Clear Rating',
    };

    setPlayerRating(track, 4);
    expect(getPlayerRating(track)).toBe(4);

    setPlayerRating(track, 0);
    expect(getPlayerRating(track)).toBe(0);
  });

  it('ignores corrupt stored ratings', () => {
    window.localStorage.setItem(playerRatingsStorageKey, 'not-json');

    expect(getPlayerRating({ contentId: 'sha256:fixture-track' })).toBe(0);
  });

  it('tolerates empty now-playing state', () => {
    expect(getPlayerRatingKey(null)).toBe('');
    expect(getPlayerRating(null)).toBe(0);
  });
});
