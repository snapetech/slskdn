// <copyright file="soulseekDiscovery.js" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import api from './api';

export const addInterest = ({ item }) =>
  api.post('/soulseek/interests', { item });

export const removeInterest = ({ item }) =>
  api.delete(`/soulseek/interests/${encodeURIComponent(item)}`);

export const addHatedInterest = ({ item }) =>
  api.post('/soulseek/hated-interests', { item });

export const removeHatedInterest = ({ item }) =>
  api.delete(`/soulseek/hated-interests/${encodeURIComponent(item)}`);

export const getRecommendations = () => api.get('/soulseek/recommendations');

export const getGlobalRecommendations = () =>
  api.get('/soulseek/recommendations/global');

export const getUserInterests = ({ username }) =>
  api.get(`/soulseek/users/${encodeURIComponent(username)}/interests`);

export const getSimilarUsers = () => api.get('/soulseek/users/similar');

export const getItemRecommendations = ({ item }) =>
  api.get(`/soulseek/items/${encodeURIComponent(item)}/recommendations`);

export const getItemSimilarUsers = ({ item }) =>
  api.get(`/soulseek/items/${encodeURIComponent(item)}/similar-users`);
