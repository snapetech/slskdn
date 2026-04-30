// <copyright file="searchResultDeduplication.js" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

const mediaExtensions = new Set([
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

const normalizePath = (value = '') =>
  value
    .toLowerCase()
    .replace(/\\/gu, '/')
    .replace(/[^\d a-z./_-]+/gu, ' ')
    .replace(/\s+/gu, ' ')
    .trim();

const getExtension = (filename = '') => {
  const lastSegment = filename.split(/[\\/]/u).pop() || '';
  const index = lastSegment.lastIndexOf('.');
  return index >= 0 ? lastSegment.slice(index + 1).toLowerCase() : '';
};

const getBasename = (filename = '') =>
  normalizePath(filename.split(/[\\/]/u).pop() || filename);

const getFiles = (response = {}) => [
  ...(Array.isArray(response.files) ? response.files : []),
  ...(Array.isArray(response.lockedFiles) ? response.lockedFiles : []),
];

const getProviderLabels = (response = {}) => {
  const providers = new Set(response.sourceProviders || []);

  if (response.primarySource) {
    providers.add(response.primarySource);
  }

  if (providers.size === 0) {
    providers.add('soulseek');
  }

  return [...providers].sort();
};

const getResponseSignature = (response = {}) => {
  const mediaFiles = getFiles(response)
    .filter((file) => mediaExtensions.has(getExtension(file.filename)))
    .map((file) => ({
      basename: getBasename(file.filename),
      size: file.size || 0,
    }))
    .sort((left, right) => left.basename.localeCompare(right.basename));

  if (mediaFiles.length === 0) {
    return '';
  }

  const visibleFiles = mediaFiles.slice(0, 20);
  const totalSize = mediaFiles.reduce((sum, file) => sum + file.size, 0);
  const sizeBucket = Math.round(totalSize / 1_000_000);

  return [
    mediaFiles.length,
    sizeBucket,
    ...visibleFiles.map((file) => `${file.basename}:${Math.round(file.size / 10_000)}`),
  ].join('|');
};

const summarizeGroup = (group) => {
  const providers = new Set();
  const usernames = new Set();

  group.forEach((response) => {
    getProviderLabels(response).forEach((provider) => providers.add(provider));
    if (response.username) {
      usernames.add(response.username);
    }
  });

  return {
    candidateCount: group.length,
    foldedCount: Math.max(group.length - 1, 0),
    providers: [...providers].sort(),
    usernames: [...usernames].sort(),
  };
};

export const deduplicateSearchResponses = ({
  enabled = true,
  responses = [],
} = {}) => {
  if (!enabled || responses.length === 0) {
    return {
      foldedCount: 0,
      groups: [],
      responses,
    };
  }

  const grouped = responses.reduce((map, response) => {
    const key = getResponseSignature(response);
    if (!key) {
      return map;
    }

    if (!map.has(key)) {
      map.set(key, []);
    }

    map.get(key).push(response);
    return map;
  }, new Map());

  const duplicates = new Map(
    [...grouped.entries()].filter(([, group]) => group.length > 1),
  );

  if (duplicates.size === 0) {
    return {
      foldedCount: 0,
      groups: [],
      responses,
    };
  }

  const keptKeys = new Set();
  const groups = [];
  let foldedCount = 0;

  const deduplicated = responses.reduce((list, response) => {
    const key = getResponseSignature(response);
    const group = duplicates.get(key);

    if (!group) {
      list.push(response);
      return list;
    }

    if (keptKeys.has(key)) {
      foldedCount += 1;
      return list;
    }

    keptKeys.add(key);
    const duplicateGroup = {
      key,
      ...summarizeGroup(group),
    };
    groups.push(duplicateGroup);
    list.push({
      ...response,
      duplicateGroup,
      foldedCandidates: group.slice(1),
    });

    return list;
  }, []);

  return {
    foldedCount,
    groups,
    responses: deduplicated,
  };
};
