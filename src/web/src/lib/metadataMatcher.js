const audioExtensions = new Set(['aac', 'aiff', 'alac', 'ape', 'flac', 'm4a', 'mp3', 'ogg', 'opus', 'wav']);

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

  return {
    album,
    artist,
    confidence: Number(confidence.toFixed(2)),
    evidence,
    status: confidence >= 0.75 ? 'Strong Match' : 'Needs Review',
    title,
    trackNumber,
    warnings,
  };
};
