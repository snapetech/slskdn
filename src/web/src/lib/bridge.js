import api from './api';

/**
 * Bridge API library for legacy client compatibility.
 */

export const getConfig = async () => {
  const response = await api.get('/bridge/admin/config');
  return response.data;
};

export const updateConfig = async (config) => {
  const response = await api.put('/bridge/admin/config', config);
  return response.data;
};

export const getDashboard = async () => {
  const response = await api.get('/bridge/admin/dashboard');
  return response.data;
};

export const getClients = async () => {
  const response = await api.get('/bridge/admin/clients');
  return response.data?.clients || [];
};

export const getStats = async () => {
  const response = await api.get('/bridge/admin/stats');
  return response.data;
};

export const getStatus = async () => {
  const response = await api.get('/bridge/status');
  return response.data;
};

export const startBridge = async () => {
  const response = await api.post('/bridge/start');
  return response.data;
};

export const stopBridge = async () => {
  const response = await api.post('/bridge/stop');
  return response.data;
};

export const getTransferProgress = async (transferId) => {
  const response = await api.get(`/bridge/transfer/${transferId}/progress`);
  return response.data;
};
