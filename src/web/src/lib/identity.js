// Identity & Friends API client

import api from './api';

// Profile API
export const getMyProfile = () => api.get('/profile/me');
export const updateMyProfile = (data) => api.put('/profile/me', data);
export const getProfile = (peerId) =>
  api.get(`/profile/${encodeURIComponent(peerId)}`);
export const createInvite = (data) => api.post('/profile/invite', data);

// Contacts API
export const getContacts = () => api.get('/contacts');
export const getContact = (id) => api.get(`/contacts/${id}`);
export const addContactFromInvite = (data) =>
  api.post('/contacts/from-invite', data);
export const addContactFromDiscovery = (data) =>
  api.post('/contacts/from-discovery', data);
export const updateContact = (id, data) => api.put(`/contacts/${id}`, data);
export const deleteContact = (id) => api.delete(`/contacts/${id}`);
export const getNearby = () => api.get('/contacts/nearby');
