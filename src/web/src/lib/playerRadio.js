const normalizeText = (value = '') => String(value).trim();

const unique = (values) => [...new Set(values.filter(Boolean))];

const quoteIfNeeded = (value) => {
  const normalized = normalizeText(value);
  if (!normalized) return '';
  return /\s/u.test(normalized) ? `"${normalized}"` : normalized;
};

const getTagValues = (track = {}) => {
  const tags = Array.isArray(track.tags) ? track.tags.map(normalizeText) : [];
  if (tags.filter(Boolean).length > 0) return tags;

  const genres = Array.isArray(track.genres) ? track.genres.map(normalizeText) : [];
  if (genres.filter(Boolean).length > 0) return genres;

  if (track.genre) return [normalizeText(track.genre)];
  return [];
};

export const buildPlayerRadioPlan = (track = {}) => {
  if (!track) {
    return {
      basis: [],
      primaryQuery: '',
      queries: [],
      ready: false,
      seedLabel: 'No track selected',
    };
  }

  const artist = normalizeText(track.artist);
  const title = normalizeText(track.title || track.fileName);
  const album = normalizeText(track.album);
  const tags = unique(getTagValues(track));
  const trackQuery = unique([artist, title]).join(' ');
  const artistQuery = artist;
  const albumQuery = unique([artist, album]).join(' ');
  const genreQuery = unique([artist, tags[0]]).join(' ');

  const queries = unique([
    trackQuery,
    albumQuery,
    genreQuery,
    artistQuery,
  ]).map((query, index) => ({
    id: `radio-query-${index + 1}`,
    query,
    reason:
      query === trackQuery
        ? 'Similar track seed'
        : query === albumQuery
          ? 'Album neighborhood'
          : query === genreQuery
            ? 'Artist and genre seed'
            : 'Artist radio seed',
  }));

  return {
    basis: [
      artist ? `Artist: ${artist}` : '',
      title ? `Track: ${title}` : '',
      album ? `Album: ${album}` : '',
      tags.length > 0 ? `Tags: ${tags.slice(0, 3).join(', ')}` : '',
    ].filter(Boolean),
    primaryQuery: queries[0]?.query || '',
    queries,
    ready: queries.length > 0,
    seedLabel: unique([artist, title]).join(' - ') || title || artist || 'Untitled seed',
  };
};

export const buildPlayerRadioSearchPath = (query) => {
  const normalized = normalizeText(query);
  return normalized ? `/searches?q=${encodeURIComponent(normalized)}` : '/searches';
};

export const getPlayerRadioCopyText = (plan) => {
  if (!plan?.ready) return '';

  return [
    `Smart radio seed: ${plan.seedLabel}`,
    ...plan.queries.map((item) => `${item.reason}: ${quoteIfNeeded(item.query)}`),
  ].join('\n');
};
