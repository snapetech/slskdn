// Collections & ShareGroups API client

import api from './api';

// ShareGroups
export const getShareGroups = () => api.get('/sharegroups');
export const getShareGroup = (id) => api.get(`/sharegroups/${id}`);
export const createShareGroup = (data) => api.post('/sharegroups', data);
export const updateShareGroup = (id, data) => api.put(`/sharegroups/${id}`, data);
export const deleteShareGroup = (id) => api.delete(`/sharegroups/${id}`);
export const getShareGroupMembers = (id, detailed = false) => 
  api.get(`/sharegroups/${id}/members${detailed ? '?detailed=true' : ''}`);
export const addShareGroupMember = (id, data) => api.post(`/sharegroups/${id}/members`, data);
export const removeShareGroupMember = (id, userId) => api.delete(`/sharegroups/${id}/members/${encodeURIComponent(userId)}`);

// Collections
export const getCollections = () => api.get('/collections');
export const getCollection = (id) => api.get(`/collections/${id}`);
export const createCollection = (data) => api.post('/collections', data);
export const updateCollection = (id, data) => api.put(`/collections/${id}`, data);
export const deleteCollection = (id) => api.delete(`/collections/${id}`);
export const getCollectionItems = (id) => api.get(`/collections/${id}/items`);
export const addCollectionItem = (id, data) => api.post(`/collections/${id}/items`, data);
export const updateCollectionItem = (itemId, data) => api.put(`/collections/items/${itemId}`, data);
export const removeCollectionItem = (itemId) => api.delete(`/collections/items/${itemId}`);
export const reorderCollectionItems = (id, itemIds) => api.put(`/collections/${id}/items/reorder`, { itemIds });

// Share Grants (Shares)
export const getShares = () => api.get('/shares');
export const getShare = (id) => api.get(`/shares/${id}`);
export const createShare = (data) => api.post('/shares', data);
export const updateShare = (id, data) => api.put(`/shares/${id}`, data);
export const deleteShare = (id) => api.delete(`/shares/${id}`);
export const createShareToken = (id, expiresInSeconds) => api.post(`/shares/${id}/token`, { expiresInSeconds });
export const getShareManifest = (id, token) => {
  const url = token 
    ? `/shares/${id}/manifest?token=${encodeURIComponent(token)}`
    : `/shares/${id}/manifest`;
  return api.get(url);
};
