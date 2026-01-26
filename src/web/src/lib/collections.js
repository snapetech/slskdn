// Collections & ShareGroups API client

import api from './api';

const base = '/api/v0';

// ShareGroups
export const getShareGroups = () => api.get(`${base}/sharegroups`);
export const getShareGroup = (id) => api.get(`${base}/sharegroups/${id}`);
export const createShareGroup = (data) => api.post(`${base}/sharegroups`, data);
export const updateShareGroup = (id, data) => api.put(`${base}/sharegroups/${id}`, data);
export const deleteShareGroup = (id) => api.delete(`${base}/sharegroups/${id}`);
export const getShareGroupMembers = (id, detailed = false) => 
  api.get(`${base}/sharegroups/${id}/members${detailed ? '?detailed=true' : ''}`);
export const addShareGroupMember = (id, data) => api.post(`${base}/sharegroups/${id}/members`, data);
export const removeShareGroupMember = (id, userId) => api.delete(`${base}/sharegroups/${id}/members/${encodeURIComponent(userId)}`);

// Collections
export const getCollections = () => api.get(`${base}/collections`);
export const getCollection = (id) => api.get(`${base}/collections/${id}`);
export const createCollection = (data) => api.post(`${base}/collections`, data);
export const updateCollection = (id, data) => api.put(`${base}/collections/${id}`, data);
export const deleteCollection = (id) => api.delete(`${base}/collections/${id}`);
export const getCollectionItems = (id) => api.get(`${base}/collections/${id}/items`);
export const addCollectionItem = (id, data) => api.post(`${base}/collections/${id}/items`, data);
export const updateCollectionItem = (itemId, data) => api.put(`${base}/collections/items/${itemId}`, data);
export const removeCollectionItem = (itemId) => api.delete(`${base}/collections/items/${itemId}`);
export const reorderCollectionItems = (id, itemIds) => api.put(`${base}/collections/${id}/items/reorder`, { itemIds });

// Share Grants (Shares)
export const getShares = () => api.get(`${base}/shares`);
export const getShare = (id) => api.get(`${base}/shares/${id}`);
export const createShare = (data) => api.post(`${base}/shares`, data);
export const updateShare = (id, data) => api.put(`${base}/shares/${id}`, data);
export const deleteShare = (id) => api.delete(`${base}/shares/${id}`);
export const createShareToken = (id, expiresInSeconds) => api.post(`${base}/shares/${id}/token`, { expiresInSeconds });
export const getShareManifest = (id, token) => {
  const url = token 
    ? `${base}/shares/${id}/manifest?token=${encodeURIComponent(token)}`
    : `${base}/shares/${id}/manifest`;
  return api.get(url);
};
