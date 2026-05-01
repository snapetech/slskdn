const audioExtensions = new Set(['aac', 'aiff', 'alac', 'ape', 'flac', 'm4a', 'mp3', 'ogg', 'opus', 'wav']);
const versionTerms = [
  'acoustic',
  'clean',
  'deluxe',
  'demo',
  'explicit',
  'instrumental',
  'live',
  'radio edit',
  'remaster',
  'remastered',
  'remix',
];

const stripExtension = (fileName) => {
  const lastDot = fileName.lastIndexOf('.');
  return lastDot > 0 ? fileName.slice(0, lastDot) : fileName;
};

const getExtension = (fileName) => {
  const lastDot = fileName.lastIndexOf('.');
  return lastDot > 0 ? fileName.slice(lastDot + 1).toLowerCase() : '';
};

const cleanPart = (value) =>
  `${value || ''}`
    .replace(/[_]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();

export const normalizeMetadataText = (value = '') =>
  `${value || ''}`
    .normalize('NFKD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/&/g, ' and ')
    .replace(/[^\p{Letter}\p{Number}]+/gu, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .toLowerCase();

const getTokens = (value) => normalizeMetadataText(value).split(' ').filter(Boolean);

const getTokenScore = (left, right) => {
  const leftTokens = getTokens(left);
  const rightTokens = new Set(getTokens(right));
  if (leftTokens.length === 0 || rightTokens.size === 0) return 0;

  const matched = leftTokens.filter((token) => rightTokens.has(token)).length;
  return matched / Math.max(leftTokens.length, rightTokens.size);
};

export const getVersionTags = (value = '') => {
  const normalized = normalizeMetadataText(value);
  return versionTerms.filter((term) => normalized.includes(term));
};

const getDurationScore = (expectedMs, candidateMs) => {
  if (!expectedMs || !candidateMs) {
    return {
      reason: 'Duration evidence unavailable.',
      score: 0.5,
    };
  }

  const delta = Math.abs(expectedMs - candidateMs);
  if (delta <= 2_000) return { reason: 'Duration matches within 2 seconds.', score: 1 };
  if (delta <= 5_000) return { reason: 'Duration is close but not exact.', score: 0.75 };
  if (delta <= 15_000) return { reason: 'Duration differs enough to require review.', score: 0.35 };
  return { reason: 'Duration mismatch is too large for an automatic match.', score: 0 };
};

export const getConfidenceBand = (confidence) => {
  if (confidence >= 0.9) return 'Auto';
  if (confidence >= 0.65) return 'Review';
  return 'Reject';
};

const parseTrackNumber = (value) => {
  const match = `${value || ''}`.match(/^\s*(?:disc\s*)?(\d{1,2})(?:\s*[-._)]|\s+|$)/i);
  return match ? Number.parseInt(match[1], 10) : null;
};

const removeLeadingTrackNumber = (value) =>
  cleanPart(`${value || ''}`.replace(/^\s*(?:disc\s*)?\d{1,2}(?:\s*[-._)]|\s+)/i, ''));

const splitMetadataParts = (fileName) =>
  stripExtension(fileName)
    .split(/\s+-\s+|\s+--\s+|\s+\u2013\s+|\s+\u2014\s+/)
    .map(cleanPart)
    .filter(Boolean);

export const buildMetadataMatch = (item) => {
  const fileName = item.fileName || item.name || '';
  const extension = getExtension(fileName);
  const parts = splitMetadataParts(fileName);
  const warnings = [];
  const evidence = [];
  const trackNumber =
    parts.map(parseTrackNumber).find((number) => number !== null) ??
    parseTrackNumber(stripExtension(fileName));
  let artist = '';
  let album = '';
  let title = '';

  if (parts.length >= 4) {
    artist = parts[0];
    album = parts[1];
    title = removeLeadingTrackNumber(parts.slice(2).join(' - '));
    evidence.push('Parsed artist, album, and title from separated filename parts.');
  } else if (parts.length === 3) {
    artist = parts[0];
    album = trackNumber ? '' : parts[1];
    title = removeLeadingTrackNumber(trackNumber ? parts.slice(1).join(' - ') : parts[2]);
    evidence.push(
      album
        ? 'Parsed artist, album, and title from filename.'
        : 'Parsed artist and title with leading track number.',
    );
  } else if (parts.length === 2) {
    artist = removeLeadingTrackNumber(parts[0]);
    title = removeLeadingTrackNumber(parts[1]);
    evidence.push('Parsed artist and title from filename.');
  } else {
    title = removeLeadingTrackNumber(parts[0] || stripExtension(fileName));
    warnings.push('Filename did not include a clear artist separator.');
  }

  if (trackNumber) {
    evidence.push(`Detected track number ${trackNumber}.`);
  }

  if (extension) {
    evidence.push(`Detected .${extension} file extension.`);
  }

  const isKnownAudio = audioExtensions.has(extension) || `${item.type || ''}`.startsWith('audio/');
  if (!isKnownAudio) {
    warnings.push('File extension or MIME type is not a known audio format.');
  }

  if (!artist) {
    warnings.push('Artist could not be inferred confidently.');
  }

  if (!title) {
    warnings.push('Title could not be inferred confidently.');
  }

  const confidence = Math.min(
    0.98,
    0.2 +
      (artist ? 0.25 : 0) +
      (title ? 0.25 : 0) +
      (album ? 0.1 : 0) +
      (trackNumber ? 0.08 : 0) +
      (isKnownAudio ? 0.1 : 0),
  );

  const band = getConfidenceBand(confidence);

  return {
    album,
    artist,
    band,
    confidence: Number(confidence.toFixed(2)),
    evidence,
    strongestEvidence: evidence[0] || 'No strong evidence.',
    status: confidence >= 0.75 ? 'Strong Match' : 'Needs Review',
    title,
    trackNumber,
    warnings,
    weakestEvidence: warnings[0] || 'No weak evidence.',
  };
};

export const scoreMetadataCandidate = (expected = {}, candidate = {}) => {
  const titleScore = getTokenScore(expected.title, candidate.title || candidate.fileName);
  const artistScore = getTokenScore(expected.artist, candidate.artist || candidate.fileName);
  const albumScore = expected.album
    ? getTokenScore(expected.album, candidate.album || candidate.fileName)
    : 0.5;
  const duration = getDurationScore(expected.durationMs, candidate.durationMs);
  const expectedVersions = getVersionTags(
    [expected.title, expected.album, expected.version].filter(Boolean).join(' '),
  );
  const candidateVersions = getVersionTags(
    [candidate.title, candidate.album, candidate.fileName, candidate.version]
      .filter(Boolean)
      .join(' '),
  );
  const versionMismatch = expectedVersions.some(
    (tag) => !candidateVersions.includes(tag),
  ) || candidateVersions.some((tag) => !expectedVersions.includes(tag));
  const identifierMatches = [
    expected.isrc && expected.isrc === candidate.isrc,
    expected.mbid && expected.mbid === candidate.mbid,
    expected.acoustId && expected.acoustId === candidate.acoustId,
    expected.providerId && expected.providerId === candidate.providerId,
  ].filter(Boolean).length;
  const shortTitle = normalizeMetadataText(expected.title).length <= 4;
  let confidence =
    titleScore * 0.34 +
    artistScore * 0.24 +
    albumScore * 0.12 +
    duration.score * 0.16 +
    Math.min(identifierMatches * 0.12, 0.2);

  const warnings = [];
  const evidence = [
    `Title score ${Math.round(titleScore * 100)}%.`,
    `Artist score ${Math.round(artistScore * 100)}%.`,
    `Album score ${Math.round(albumScore * 100)}%.`,
    duration.reason,
  ];

  if (identifierMatches > 0) {
    evidence.unshift(`${identifierMatches} external identifier match${identifierMatches === 1 ? '' : 'es'}.`);
  }

  if (versionMismatch) {
    confidence -= 0.18;
    warnings.push('Version tags differ between expected metadata and candidate.');
  }

  if (shortTitle && identifierMatches === 0 && duration.score < 1) {
    confidence = Math.min(confidence, 0.64);
    warnings.push('Short title requires exact duration or identifier evidence.');
  }

  confidence = Math.min(Math.max(confidence, 0), 0.99);

  return {
    albumScore: Number(albumScore.toFixed(2)),
    artistScore: Number(artistScore.toFixed(2)),
    band: getConfidenceBand(confidence),
    confidence: Number(confidence.toFixed(2)),
    evidence,
    strongestEvidence: evidence[0],
    titleScore: Number(titleScore.toFixed(2)),
    versionMismatch,
    versionTags: [...new Set([...expectedVersions, ...candidateVersions])],
    warnings,
    weakestEvidence: warnings[0] || (duration.score < 0.5 ? duration.reason : 'No weak evidence.'),
  };
};
