import api from './api';

export const getAll = async ({ unAcknowledgedOnly = false } = {}) => {
  const query = unAcknowledgedOnly ? '?unAcknowledgedOnly=true' : '';
  return (await api.get(`/conversations${query}`)).data;
};

export const get = async ({ username }) => {
  return (await api.get(`/conversations/${encodeURIComponent(username)}`)).data;
};

export const acknowledge = ({ username }) => {
  return api.put(`/conversations/${encodeURIComponent(username)}`);
};

export const send = ({ username, message }) => {
  return api.post(
    `/conversations/${encodeURIComponent(username)}`,
    JSON.stringify(message),
  );
};

export const sendBatch = ({ message, usernames }) => {
  return api.post('/conversations/batch', {
    message,
    usernames,
  });
};

export const remove = ({ username }) => {
  return api.delete(`/conversations/${encodeURIComponent(username)}`);
};
