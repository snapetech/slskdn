import api from './api';

export const startScan = (libraryPath) =>
  api.post('/api/library/health/scans', {
    includeSubdirectories: true,
    libraryPath,
  });

export const getScanStatus = (scanId) =>
  api.get(`/api/library/health/scans/${scanId}`);

export const getSummary = (libraryPath) =>
  api.get(
    `/api/library/health/summary?libraryPath=${encodeURIComponent(libraryPath)}`,
  );

export const getIssues = (filter = {}) => {
  const parameters = new URLSearchParams();
  if (filter.libraryPath) parameters.append('libraryPath', filter.libraryPath);
  if (filter.limit) parameters.append('limit', filter.limit);
  if (filter.offset) parameters.append('offset', filter.offset);
  return api.get(`/api/library/health/issues?${parameters.toString()}`);
};

export const getIssuesByType = (libraryPath = null) => {
  const parameters = libraryPath
    ? `?libraryPath=${encodeURIComponent(libraryPath)}`
    : '';
  return api.get(`/api/library/health/issues/by-type${parameters}`);
};

export const getIssuesByArtist = (limit = 20) =>
  api.get(`/api/library/health/issues/by-artist?limit=${limit}`);

export const getIssuesByRelease = (limit = 20) =>
  api.get(`/api/library/health/issues/by-release?limit=${limit}`);

export const updateIssueStatus = (issueId, status) =>
  api.patch(`/api/library/health/issues/${issueId}`, { status });

export const createRemediationJob = (issueIds) =>
  api.post(`/api/library/health/issues/fix`, { issueIds });

export default {
  createRemediationJob,
  getIssues,
  getIssuesByArtist,
  getIssuesByRelease,
  getIssuesByType,
  getScanStatus,
  getSummary,
  startScan,
  updateIssueStatus,
};
