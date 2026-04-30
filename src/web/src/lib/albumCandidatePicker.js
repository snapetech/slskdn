import { getDirectoryName } from './util';

const audioExtensions = new Set([
  'aac',
  'aif',
  'aiff',
  'alac',
  'ape',
  'flac',
  'm4a',
  'mp3',
  'ogg',
  'opus',
  'wav',
  'wma',
  'wv',
]);

const losslessExtensions = new Set([
  'aif',
  'aiff',
  'alac',
  'ape',
  'flac',
  'wav',
  'wv',
]);

const getExtension = (filename = '') => {
  const index = filename.lastIndexOf('.');
  return index >= 0 ? filename.slice(index + 1).toLowerCase() : '';
};

const getBasename = (path = '') =>
  path.split(/[\\/]/u).filter(Boolean).pop() || path;

const normalizeTitle = (value = '') =>
  value
    .toLowerCase()
    .replace(/\[[^\]]*\]/gu, ' ')
    .replace(/\([^)]*\)/gu, ' ')
    .replace(/[^\d a-z]+/gu, ' ')
    .replace(/\b(cd|disc|disk)\s*\d+\b/gu, ' ')
    .replace(/\s+/gu, ' ')
    .trim();

const getTrackNumber = (filename = '') => {
  const basename = getBasename(filename);
  const match = basename.match(/(?:^|[^\d])(\d{1,2})(?:\s*[-_. )]|$)/u);
  return match ? Number.parseInt(match[1], 10) : null;
};

const getVisibleFiles = (response) => [
  ...(Array.isArray(response.files) ? response.files : []),
  ...(Array.isArray(response.lockedFiles) ? response.lockedFiles : []),
];

const toCandidate = (group) => {
  const trackNumbers = [...group.trackNumbers].sort((a, b) => a - b);
  const highestTrackNumber = trackNumbers.at(-1) || group.trackCount;
  const expectedTrackCount = Math.max(highestTrackNumber, group.trackCount);
  const completenessRatio = expectedTrackCount > 0
    ? Math.min(group.trackCount / expectedTrackCount, 1)
    : 0;
  const score = Math.round(
    Math.min(group.bestCandidateScore, 100) * 0.45 +
      Math.min(group.trackCount, 14) * 3 +
      Math.min(group.losslessCount, 10) * 2 +
      Math.min(group.sourceCount, 4) * 4 +
      completenessRatio * 18,
  );

  const reasons = [];
  if (group.losslessCount > 0) reasons.push(`${group.losslessCount} lossless file${group.losslessCount === 1 ? '' : 's'}`);
  if (group.sourceCount > 1) reasons.push(`${group.sourceCount} sources`);
  if (trackNumbers.length >= 3) reasons.push('numbered track run');
  if (completenessRatio >= 0.8) reasons.push('high folder completeness');

  return {
    ...group,
    completenessRatio,
    expectedTrackCount,
    reasons,
    score: Math.min(score, 100),
    trackNumbers,
  };
};

export const buildAlbumCandidates = ({
  responses = [],
  searchText = '',
} = {}) => {
  const groups = new Map();
  const normalizedSearch = normalizeTitle(searchText);

  responses.forEach((response) => {
    getVisibleFiles(response).forEach((file) => {
      const extension = getExtension(file.filename);
      if (!audioExtensions.has(extension)) {
        return;
      }

      const directory = getDirectoryName(file.filename);
      const albumTitle = getBasename(directory);
      const normalizedAlbum = normalizeTitle(albumTitle);
      if (!normalizedAlbum) {
        return;
      }

      const key = normalizedAlbum;
      const existing = groups.get(key) || {
        albumTitle,
        bestCandidateScore: 0,
        directories: new Set(),
        files: [],
        key,
        losslessCount: 0,
        sourceCount: 0,
        sources: new Set(),
        trackCount: 0,
        trackNumbers: new Set(),
      };

      existing.bestCandidateScore = Math.max(
        existing.bestCandidateScore,
        response.candidateRank?.score ?? response.smartScore ?? 0,
      );
      existing.directories.add(directory);
      existing.files.push(file);
      existing.sources.add(response.username);
      existing.sourceCount = existing.sources.size;
      existing.trackCount += 1;
      if (
        losslessExtensions.has(extension) ||
        (file.bitDepth && file.sampleRate)
      ) {
        existing.losslessCount += 1;
      }

      const trackNumber = getTrackNumber(file.filename);
      if (trackNumber) {
        existing.trackNumbers.add(trackNumber);
      }

      groups.set(key, existing);
    });
  });

  return [...groups.values()]
    .filter((group) => group.trackCount >= 3)
    .map((group) => ({
      ...toCandidate(group),
      directories: [...group.directories].slice(0, 4),
      files: group.files.slice(0, 8),
      searchOverlap:
        normalizedSearch &&
        normalizeTitle(group.albumTitle).includes(normalizedSearch)
          ? 1
          : 0,
      sources: [...group.sources].sort(),
    }))
    .sort((a, b) => b.score - a.score || b.trackCount - a.trackCount)
    .slice(0, 6);
};

export const getAlbumCandidateFilter = (candidate) => {
  if (!candidate?.albumTitle) {
    return '';
  }

  return normalizeTitle(candidate.albumTitle)
    .split(' ')
    .filter((token) => token.length > 2)
    .slice(0, 4)
    .join(' ');
};
