import api from './api';

/**
 * Get all stuck downloads that are candidates for auto-replacement
 */
export const getStuckDownloads = async () => {
  const response = await api.get('/transfers/downloads/stuck');
  return response.data;
};

/**
 * Find alternative sources for a specific stuck download
 * @param {string} username - The username of the original source
 * @param {string} filename - The full filename/path
 * @param {number} size - The expected file size
 * @param {number} threshold - Max size difference percentage (e.g., 5.0 for 5%)
 */
export const findAlternative = async ({ username, filename, size, threshold = 5.0 }) => {
  const response = await api.post('/transfers/downloads/find-alternative', {
    username,
    filename,
    size,
    threshold,
  });
  return response.data;
};

/**
 * Replace a stuck download with an alternative source
 * @param {string} originalId - The ID of the stuck download to replace
 * @param {string} newUsername - The username of the alternative source
 * @param {string} newFilename - The filename from the alternative source
 * @param {number} newSize - The size of the alternative file
 */
export const replaceDownload = async ({ originalId, originalUsername, newUsername, newFilename, newSize }) => {
  const response = await api.post('/transfers/downloads/replace', {
    originalId,
    originalUsername,
    newUsername,
    newFilename,
    newSize,
  });
  return response.data;
};

/**
 * Process all stuck downloads and attempt auto-replacement
 * @param {number} threshold - Max size difference percentage for auto-replacement
 */
export const processStuckDownloads = async ({ threshold = 5.0 }) => {
  const response = await api.post('/transfers/downloads/auto-replace', {
    threshold,
  });
  return response.data;
};

/**
 * Get auto-replace configuration/status
 */
export const getAutoReplaceStatus = async () => {
  const response = await api.get('/transfers/downloads/auto-replace/status');
  return response.data;
};

