// <copyright file="albumDecisionRules.js" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import { getLocalStorageItem, setLocalStorageItem } from './storage';

const STORAGE_KEY = 'slskdn.albumDecisionRules';
const MAX_RULES = 50;

const normalizeText = (value = '') =>
  value
    .toLowerCase()
    .replace(/[^\d a-z]+/gu, ' ')
    .replace(/\s+/gu, ' ')
    .trim();

const parseRules = () => {
  try {
    const parsed = JSON.parse(getLocalStorageItem(STORAGE_KEY, '[]'));
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
};

export const getAlbumDecisionRules = () => parseRules();

export const buildAlbumDecisionRule = ({
  candidate,
  createdAt = new Date().toISOString(),
  searchText = '',
} = {}) => {
  const albumKey = normalizeText(candidate?.albumTitle || searchText);
  const searchKey = normalizeText(searchText);
  const formatPolicy = (candidate?.formatMix || [])
    .map((item) => `${item.format}:${item.count}`)
    .join(',');

  return {
    albumKey,
    albumTitle: candidate?.albumTitle || searchText,
    createdAt,
    expectedTrackCount: candidate?.expectedTrackCount || 0,
    formatPolicy,
    id: `${albumKey || searchKey}:${candidate?.expectedTrackCount || 0}:${formatPolicy}`,
    minCompleteness: candidate?.completenessRatio || 0,
    notes: [
      ...(candidate?.warnings || []).map((warning) => `warn:${warning}`),
      ...(candidate?.substitutionOptions || []).map(
        (option) =>
          `substitute:track-${option.trackNumber}:${option.optionCount}-options`,
      ),
    ],
    searchKey,
    sourceCount: candidate?.sourceCount || 0,
    substitutionTracks: (candidate?.substitutionOptions || []).map(
      (option) => option.trackNumber,
    ),
    warningCount: candidate?.warnings?.length || 0,
  };
};

export const saveAlbumDecisionRule = ({ candidate, searchText } = {}) => {
  const rule = buildAlbumDecisionRule({ candidate, searchText });
  const existing = parseRules().filter((item) => item.id !== rule.id);
  const rules = [rule, ...existing].slice(0, MAX_RULES);

  setLocalStorageItem(STORAGE_KEY, JSON.stringify(rules));

  return {
    rule,
    rules,
  };
};
