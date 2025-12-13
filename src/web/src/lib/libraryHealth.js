import api from './api';

export const startScan = (libraryPath) =>
  api.post('/api/library/health/scans', { libraryPath, includeSubdirectories: true });

export const getScanStatus = (scanId) =>
  api.get(`/api/library/health/scans/${scanId}`);

export const getSummary = (libraryPath) =>
  api.get(`/api/library/health/summary?libraryPath=${encodeURIComponent(libraryPath)}`);

export const getIssues = (filter = {}) => {
  const params = new URLSearchParams();
  if (filter.libraryPath) params.append('libraryPath', filter.libraryPath);
  if (filter.limit) params.append('limit', filter.limit);
  if (filter.offset) params.append('offset', filter.offset);
  return api.get(`/api/library/health/issues?${params.toString()}`);
};

export const getIssuesByType = (libraryPath = null) => {
  const params = libraryPath ? `?libraryPath=${encodeURIComponent(libraryPath)}` : '';
  return api.get(`/api/library/health/issues/by-type${params}`);
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
  startScan,
  getScanStatus,
  getSummary,
  getIssues,
  getIssuesByType,
  getIssuesByArtist,
  getIssuesByRelease,
  updateIssueStatus,
  createRemediationJob,
};
















