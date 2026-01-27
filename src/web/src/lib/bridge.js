import * as api from './api';

/**
 * Bridge API library for legacy client compatibility.
 */

export const getConfig = async () => {
  const response = await api.get('/api/bridge/admin/config');
  return response;
};

export const updateConfig = async (config) => {
  const response = await api.put('/api/bridge/admin/config', config);
  return response;
};

export const getDashboard = async () => {
  const response = await api.get('/api/bridge/admin/dashboard');
  return response;
};

export const getClients = async () => {
  const response = await api.get('/api/bridge/admin/clients');
  return response.clients || [];
};

export const getStats = async () => {
  const response = await api.get('/api/bridge/admin/stats');
  return response;
};

export const getStatus = async () => {
  const response = await api.get('/api/bridge/status');
  return response;
};

export const startBridge = async () => {
  const response = await api.post('/api/bridge/start');
  return response;
};

export const stopBridge = async () => {
  const response = await api.post('/api/bridge/stop');
  return response;
};

export const getTransferProgress = async (transferId) => {
  const response = await api.get(`/api/bridge/transfer/${transferId}/progress`);
  return response;
};
