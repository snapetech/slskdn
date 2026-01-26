// Identity & Friends API client

import api from './api';

const identityBase = '/api/v0';

// Profile API
export const getMyProfile = () => api.get(`${identityBase}/profile/me`);
export const updateMyProfile = (data) => api.put(`${identityBase}/profile/me`, data);
export const getProfile = (peerId) => api.get(`${identityBase}/profile/${encodeURIComponent(peerId)}`);
export const createInvite = (data) => api.post(`${identityBase}/profile/invite`, data);

// Contacts API
export const getContacts = () => api.get(`${identityBase}/contacts`);
export const getContact = (id) => api.get(`${identityBase}/contacts/${id}`);
export const addContactFromInvite = (data) => api.post(`${identityBase}/contacts/from-invite`, data);
export const addContactFromDiscovery = (data) => api.post(`${identityBase}/contacts/from-discovery`, data);
export const updateContact = (id, data) => api.put(`${identityBase}/contacts/${id}`, data);
export const deleteContact = (id) => api.delete(`${identityBase}/contacts/${id}`);
export const getNearby = () => api.get(`${identityBase}/contacts/nearby`);
