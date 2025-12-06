import api from './api';

export const getAll = async () => {
  return (await api.get('/searches')).data;
};

export const stop = ({ id }) => {
  return api.put(`/searches/${encodeURIComponent(id)}`);
};

export const remove = ({ id }) => {
  return api.delete(`/searches/${encodeURIComponent(id)}`);
};

export const removeAll = () => {
  return api.delete('/searches');
};

// User download stats for badges
export const getUserDownloadStats = async () => {
  return (await api.get('/transfers/downloads/user-stats')).data;
};

// Blocked users management (localStorage-based)
const BLOCKED_USERS_KEY = 'slskdn_blocked_users';

export const getBlockedUsers = () => {
  try {
    const blocked = localStorage.getItem(BLOCKED_USERS_KEY);
    return blocked ? JSON.parse(blocked) : [];
  } catch {
    return [];
  }
};

export const blockUser = (username) => {
  const blocked = getBlockedUsers();
  if (!blocked.includes(username)) {
    blocked.push(username);
    localStorage.setItem(BLOCKED_USERS_KEY, JSON.stringify(blocked));
  }

  return blocked;
};

export const unblockUser = (username) => {
  let blocked = getBlockedUsers();
  blocked = blocked.filter((u) => u !== username);
  localStorage.setItem(BLOCKED_USERS_KEY, JSON.stringify(blocked));
  return blocked;
};

export const isUserBlocked = (username) => {
  return getBlockedUsers().includes(username);
};

export const create = ({ id, searchText }) => {
  return api.post('/searches', { id, searchText });
};

export const getStatus = async ({ id, includeResponses = false }) => {
  return (
    await api.get(
      `/searches/${encodeURIComponent(id)}?includeResponses=${includeResponses}`,
    )
  ).data;
};

export const getResponses = async ({ id }) => {
  const response = (
    await api.get(`/searches/${encodeURIComponent(id)}/responses`)
  ).data;

  if (!Array.isArray(response)) {
    console.warn('got non-array response from searches API', response);
    return undefined;
  }

  return response;
};

const getNthMatch = (string, regex, n) => {
  const match = string.match(regex);

  if (match) {
    return Number.parseInt(match[n], 10);
  }

  return undefined;
};

// Re-implementing correctly:
const parseSize = (value, unit) => {
  const parsedNumber = Number.parseInt(value, 10);
  switch (unit?.toLowerCase()) {
    case 'gb':
      return parsedNumber * 1_024 * 1_024 * 1_024;
    case 'mb':
      return parsedNumber * 1_024 * 1_024;
    case 'kb':
      return parsedNumber * 1_024;
    default:
      return parsedNumber * 1_024 * 1_024;
  }
};

const getSizeFromRegex = (string, regex) => {
  const match = string.match(regex);
  if (match) {
    const value = match[2];
    const unit = match[3];
    if (unit) {
      return parseSize(value, unit);
    }

    return Number.parseInt(value, 10);
  }

  return undefined;
};

export const parseFiltersFromString = (string) => {
  const filters = {
    exclude: [],
    include: [],
    isCBR: false,
    isLossless: false,
    isLossy: false,
    isVBR: false,
    maxFileSize: Number.MAX_SAFE_INTEGER,
    minBitDepth: 0,
    minBitRate: 0,
    minFilesInFolder: 0,
    minFileSize: 0,
    minLength: 0,
  };

  filters.minBitRate =
    getNthMatch(string, /(minbr|minbitrate):(\d+)/iu, 2) || filters.minBitRate;
  filters.minBitDepth =
    getNthMatch(string, /(minbd|minbitdepth):(\d+)/iu, 2) ||
    filters.minBitDepth;

  filters.minFileSize =
    getSizeFromRegex(string, /(minfs|minfilesize):(\d+)(kb|mb|gb)?/iu) ||
    filters.minFileSize;

  filters.maxFileSize =
    getSizeFromRegex(string, /(maxfs|maxfilesize):(\d+)(kb|mb|gb)?/iu) ||
    filters.maxFileSize;

  filters.minLength =
    getNthMatch(string, /(minlen|minlength):(\d+)/iu, 2) || filters.minLength;
  filters.minFilesInFolder =
    getNthMatch(string, /(minfif|minfilesinfolder):(\d+)/iu, 2) ||
    filters.minFilesInFolder;

  filters.isVBR = Boolean(/isvbr/iu.test(string));
  filters.isCBR = Boolean(/iscbr/iu.test(string));
  filters.isLossless = Boolean(/islossless/iu.test(string));
  filters.isLossy = Boolean(/islossy/iu.test(string));

  const terms = string
    .toLowerCase()
    .split(' ')
    .filter(
      (term) =>
        !term.includes(':') &&
        term !== 'isvbr' &&
        term !== 'iscbr' &&
        term !== 'islossless' &&
        term !== 'islossy',
    );

  filters.include = terms.filter((term) => !term.startsWith('-'));
  filters.exclude = terms
    .filter((term) => term.startsWith('-'))
    .map((term) => term.slice(1));

  return filters;
};

// eslint-disable-next-line complexity
const filterFile = (file, filters) => {
  const {
    bitRate,
    size,
    length,
    filename,
    sampleRate,
    bitDepth,
    isVariableBitRate,
  } = file;
  const {
    isCBR,
    isVBR,
    isLossless,
    isLossy,
    minBitRate,
    minBitDepth,
    maxFileSize,
    minFileSize,
    minLength,
    include = [],
    exclude = [],
  } = filters;

  if (isCBR && (isVariableBitRate === undefined || isVariableBitRate))
    return false;
  if (isVBR && (isVariableBitRate === undefined || !isVariableBitRate))
    return false;
  if (isLossless && (!sampleRate || !bitDepth)) return false;
  if (isLossy && (sampleRate || bitDepth)) return false;
  if (bitRate < minBitRate) return false;
  if (bitDepth < minBitDepth) return false;
  if (size < minFileSize) return false;
  if (size > maxFileSize) return false;
  if (length < minLength) return false;

  if (
    include.length > 0 &&
    include.filter((term) => filename.toLowerCase().includes(term)).length !==
      include.length
  ) {
    return false;
  }

  if (exclude.some((term) => filename.toLowerCase().includes(term)))
    return false;

  return true;
};

export const filterResponse = ({
  filters = {
    exclude: [],
    include: [],
    isCBR: false,
    isLossless: false,
    isLossy: false,
    isVBR: false,
    maxFileSize: Number.MAX_SAFE_INTEGER,
    minBitDepth: 0,
    minBitRate: 0,
    minFilesInFolder: 0,
    minFileSize: 0,
    minLength: 0,
  },
  response = {
    files: [],
    lockedFiles: [],
  },
}) => {
  const { files = [], lockedFiles = [] } = response;

  if (
    response.fileCount + response.lockedFileCount <
    filters.minFilesInFolder
  ) {
    return { ...response, files: [] };
  }

  const filterFiles = (filesToFilter) =>
    filesToFilter.filter((file) => filterFile(file, filters));

  const filteredFiles = filterFiles(files);
  const filteredLockedFiles = filterFiles(lockedFiles);

  return {
    ...response,
    fileCount: filteredFiles.length,
    files: filteredFiles,
    lockedFileCount: filteredLockedFiles.length,
    lockedFiles: filteredLockedFiles,
  };
};
