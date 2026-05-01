import api from './api';
import { createSongIdHubConnection } from './hubFactory';

export const createRun = async (source) => {
  const response = await api.post('/songid/runs', { source });
  return response.data;
};

export const getRun = async (id) => {
  const response = await api.get(`/songid/runs/${encodeURIComponent(id)}`);
  return response.data;
};

export const getForensicMatrix = async (id) => {
  const response = await api.get(`/songid/runs/${encodeURIComponent(id)}/forensic-matrix`);
  return response.data;
};

export const getRuns = async (limit = 10) => {
  const response = await api.get(`/songid/runs?limit=${limit}`);
  return Array.isArray(response.data) ? response.data : [];
};

export const createHub = () => createSongIdHubConnection();
