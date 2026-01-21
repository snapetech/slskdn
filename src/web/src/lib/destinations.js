import api from './api';

export const getAll = async () => {
  return (await api.get('/destinations')).data;
};

export const getDefault = async () => {
  return (await api.get('/destinations/default')).data;
};

export const validate = async (path) => {
  return (await api.post('/destinations/validate', { path })).data;
};
