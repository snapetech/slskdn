// <copyright file="tasteRecommendations.js" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import api from './api';

export const fetchTasteRecommendations = ({
  includeGraphEvidence = true,
  includeSoulseekRecommendations = false,
  includeSourceActors = false,
  limit = 20,
  minimumTrustedSources = 2,
} = {}) =>
  api.post('/taste-recommendations', {
    includeGraphEvidence,
    includeSoulseekRecommendations,
    includeSourceActors,
    limit,
    minimumTrustedSources,
  });

export const promoteTasteRecommendationToWishlist = ({ note, workRef }) =>
  api.post('/taste-recommendations/wishlist', {
    note,
    workRef,
  });

export const subscribeTasteRecommendationReleaseRadar = ({
  artistId,
  scope = 'trusted',
  workRef,
}) =>
  api.post('/taste-recommendations/release-radar', {
    artistId,
    scope,
    workRef,
  });

export const previewTasteRecommendationGraph = ({ workRef }) =>
  api.post('/taste-recommendations/graph-preview', {
    workRef,
  });
