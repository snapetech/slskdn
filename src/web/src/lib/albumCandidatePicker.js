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

const formatLabelByExtension = {
  aac: 'AAC',
  aif: 'AIFF',
  aiff: 'AIFF',
  alac: 'ALAC',
  ape: 'APE',
  flac: 'FLAC',
  m4a: 'M4A',
  mp3: 'MP3',
  ogg: 'OGG',
  opus: 'OPUS',
  wav: 'WAV',
  wma: 'WMA',
  wv: 'WV',
};

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

const getMissingTrackNumbers = (trackNumbers, expectedTrackCount) => {
  const present = new Set(trackNumbers);
  return Array.from({ length: expectedTrackCount }, (_, index) => index + 1)
    .filter((trackNumber) => !present.has(trackNumber));
};

const getDurationVarianceSeconds = (files) => {
  const durations = files
    .map((file) => file.length || 0)
    .filter((duration) => duration > 0);

  if (durations.length < 2) {
    return 0;
  }

  return Math.max(...durations) - Math.min(...durations);
};

const summarizeFormatMix = (formatCounts) =>
  Object.entries(formatCounts)
    .sort((left, right) => right[1] - left[1] || left[0].localeCompare(right[0]))
    .map(([format, count]) => ({ count, format }));

const summarizeTrackOptions = (trackOptions) =>
  [...trackOptions.entries()]
    .sort((left, right) => left[0] - right[0])
    .map(([trackNumber, files]) => {
      const sources = [...new Set(files.map((file) => file.username).filter(Boolean))]
        .sort();
      const formats = [
        ...new Set(
          files
            .map((file) => formatLabelByExtension[getExtension(file.filename)])
            .filter(Boolean),
        ),
      ].sort();

      return {
        files: files.slice(0, 4),
        formats,
        optionCount: files.length,
        sources,
        trackNumber,
      };
    });

const buildWarnings = ({
  completenessRatio,
  durationVarianceSeconds,
  expectedTrackCount,
  formatMix,
  losslessCount,
  missingTrackNumbers,
  sourceCount,
  trackCount,
  unnumberedAudioCount,
}) => {
  const warnings = [];

  if (missingTrackNumbers.length > 0) {
    warnings.push(
      `missing tracks ${missingTrackNumbers.slice(0, 8).join(', ')}`,
    );
  }

  if (unnumberedAudioCount > 0 && trackCount > expectedTrackCount) {
    warnings.push('extra unnumbered tracks');
  }

  if (formatMix.length > 1) {
    warnings.push('mixed audio formats');
  }

  if (losslessCount > 0 && losslessCount < trackCount) {
    warnings.push('mixed lossless/lossy evidence');
  }

  if (durationVarianceSeconds >= 900) {
    warnings.push('large duration variance');
  }

  if (sourceCount === 1) {
    warnings.push('single source only');
  }

  if (completenessRatio < 0.75) {
    warnings.push('low folder completeness');
  }

  return warnings;
};

const toCandidate = (group) => {
  const trackNumbers = [...group.trackNumbers].sort((a, b) => a - b);
  const highestTrackNumber = trackNumbers.at(-1) || 0;
  const expectedTrackCount = highestTrackNumber || group.trackCount;
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
  const missingTrackNumbers = getMissingTrackNumbers(
    trackNumbers,
    expectedTrackCount,
  );
  const durationVarianceSeconds = getDurationVarianceSeconds(group.files);
  const formatMix = summarizeFormatMix(group.formatCounts);
  const trackOptions = summarizeTrackOptions(group.trackOptions);
  const substitutionOptions = trackOptions.filter(
    (option) => option.optionCount > 1,
  );
  const warnings = buildWarnings({
    completenessRatio,
    durationVarianceSeconds,
    expectedTrackCount,
    formatMix,
    losslessCount: group.losslessCount,
    missingTrackNumbers,
    sourceCount: group.sourceCount,
    trackCount: group.trackCount,
    unnumberedAudioCount: group.unnumberedAudioCount,
  });

  const reasons = [];
  if (group.losslessCount > 0) {
    reasons.push(
      `${group.losslessCount} lossless file${
        group.losslessCount === 1 ? '' : 's'
      }`,
    );
  }
  if (group.sourceCount > 1) reasons.push(`${group.sourceCount} sources`);
  if (trackNumbers.length >= 3) reasons.push('numbered track run');
  if (completenessRatio >= 0.8) reasons.push('high folder completeness');

  return {
    ...group,
    completenessRatio,
    durationVarianceSeconds,
    expectedTrackCount,
    formatMix,
    missingTrackNumbers,
    reasons,
    score: Math.min(score, 100),
    substitutionOptions,
    trackNumbers,
    trackOptions,
    warnings,
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
        formatCounts: {},
        files: [],
        key,
        losslessCount: 0,
        sourceCount: 0,
        sources: new Set(),
        trackCount: 0,
        trackNumbers: new Set(),
        trackOptions: new Map(),
        unnumberedAudioCount: 0,
      };

      existing.bestCandidateScore = Math.max(
        existing.bestCandidateScore,
        response.candidateRank?.score ?? response.smartScore ?? 0,
      );
      existing.directories.add(directory);
      const trackNumber = getTrackNumber(file.filename);
      const enrichedFile = {
        ...file,
        directory,
        format: formatLabelByExtension[extension] || extension.toUpperCase(),
        trackNumber,
        username: response.username,
      };
      existing.files.push(enrichedFile);
      const format = formatLabelByExtension[extension] || extension.toUpperCase();
      existing.formatCounts[format] = (existing.formatCounts[format] || 0) + 1;
      existing.sources.add(response.username);
      existing.sourceCount = existing.sources.size;
      existing.trackCount += 1;
      if (
        losslessExtensions.has(extension) ||
        (file.bitDepth && file.sampleRate)
      ) {
        existing.losslessCount += 1;
      }

      if (trackNumber) {
        existing.trackNumbers.add(trackNumber);
        const options = existing.trackOptions.get(trackNumber) || [];
        existing.trackOptions.set(trackNumber, [...options, enrichedFile]);
      } else {
        existing.unnumberedAudioCount += 1;
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
