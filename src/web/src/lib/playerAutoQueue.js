const normalizeText = (value = '') => String(value).trim().toLowerCase();

const getTags = (item = {}) => {
  const values = [
    ...(Array.isArray(item.tags) ? item.tags : []),
    ...(Array.isArray(item.genres) ? item.genres : []),
    item.genre,
  ];

  return values.map(normalizeText).filter(Boolean);
};

const getTitleTokens = (item = {}) =>
  normalizeText(item.title || item.fileName)
    .replace(/[^\d a-z]+/gu, ' ')
    .split(/\s+/u)
    .filter((token) => token.length > 2);

const getSimilarityScore = (current, candidate) => {
  if (!current || !candidate) return 0;

  let score = 0;
  if (normalizeText(current.artist) && normalizeText(current.artist) === normalizeText(candidate.artist)) {
    score += 4;
  }
  if (normalizeText(current.album) && normalizeText(current.album) === normalizeText(candidate.album)) {
    score += 3;
  }

  const currentTags = new Set(getTags(current));
  const sharedTags = getTags(candidate).filter((tag) => currentTags.has(tag));
  score += Math.min(sharedTags.length * 2, 4);

  const currentTokens = new Set(getTitleTokens(current));
  const sharedTitleTokens = getTitleTokens(candidate).filter((token) =>
    currentTokens.has(token));
  score += Math.min(sharedTitleTokens.length, 2);

  return score;
};

export const buildSimilarQueueCandidates = ({
  current,
  history = [],
  limit = 5,
  queue = [],
} = {}) => {
  if (!current) return [];

  const queuedIds = new Set(
    queue.map((item) => item.contentId).filter(Boolean),
  );
  const seen = new Set(queuedIds);

  return history
    .map((item, index) => ({
      index,
      item,
      score: getSimilarityScore(current, item),
    }))
    .filter((candidate) => {
      const contentId = candidate.item?.contentId;
      if (!contentId || seen.has(contentId) || candidate.score <= 0) return false;
      seen.add(contentId);
      return true;
    })
    .sort((left, right) => right.score - left.score || left.index - right.index)
    .slice(0, limit);
};
