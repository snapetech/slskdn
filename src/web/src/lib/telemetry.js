import api from './api';

export const getMetrics = async () => {
  return (await api.get('/telemetry/metrics', { headers: { Accept: 'application/json' } })).data;
};

export const getKpiMetrics = async () => {
  return (await api.get('/telemetry/metrics/kpi')).data;
};
