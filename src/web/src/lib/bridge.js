import api from './api';

/**
 * Bridge API library for legacy client compatibility.
 */

export const getConfig = async () => {
  const response = await api.get('/api/bridge/admin/config');
  return response.data;
};

export const updateConfig = async (config) => {
  const response = await api.put('/api/bridge/admin/config', config);
  return response.data;
};

export const getDashboard = async () => {
  const response = await api.get('/api/bridge/admin/dashboard');
  return response.data;
};

export const getClients = async () => {
  const response = await api.get('/api/bridge/admin/clients');
  return response.data?.clients || [];
};

export const getStats = async () => {
  const response = await api.get('/api/bridge/admin/stats');
  return response.data;
};

export const getStatus = async () => {
  const response = await api.get('/api/bridge/status');
  return response.data;
};

export const startBridge = async () => {
  const response = await api.post('/api/bridge/start');
  return response.data;
};

export const stopBridge = async () => {
  const response = await api.post('/api/bridge/stop');
  return response.data;
};

export const getTransferProgress = async (transferId) => {
  const response = await api.get(`/api/bridge/transfer/${transferId}/progress`);
  return response.data;
};
