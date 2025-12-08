import api from './api';

export const getNote = ({ username }) => {
  return api.get(`/users/notes/${encodeURIComponent(username)}`);
};

export const setNote = ({ username, note, color, icon, isHighPriority }) => {
  return api.post('/users/notes', {
    color,
    icon,
    isHighPriority,
    note,
    username,
  });
};

export const deleteNote = ({ username }) => {
  return api.delete(`/users/notes/${encodeURIComponent(username)}`);
};

export const getAllNotes = () => {
  return api.get('/users/notes');
};
