import api from './api';

export const getExternalVisualizerStatus = async () =>
  (await api.get('/player/external-visualizer')).data;

export const launchExternalVisualizer = async () =>
  (await api.post('/player/external-visualizer/launch')).data;
