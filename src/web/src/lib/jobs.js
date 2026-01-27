// <copyright file="jobs.js" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import api from './api';

/**
 * Get all jobs with optional filtering, pagination, and sorting.
 * @param {Object} options - Query parameters
 * @param {string} [options.type] - Filter by job type (discography, label_crate)
 * @param {string} [options.status] - Filter by status (pending, running, completed, failed)
 * @param {number} [options.limit] - Maximum number of jobs to return
 * @param {number} [options.offset] - Number of jobs to skip
 * @param {string} [options.sortBy] - Field to sort by (status, created_at, id)
 * @param {string} [options.sortOrder] - Sort order (asc, desc)
 * @returns {Promise<Object>} Jobs response with pagination info
 */
export const getJobs = async ({
  type,
  status,
  limit,
  offset,
  sortBy,
  sortOrder,
} = {}) => {
  const params = new URLSearchParams();
  if (type) params.append('type', type);
  if (status) params.append('status', status);
  if (limit) params.append('limit', limit.toString());
  if (offset) params.append('offset', offset.toString());
  if (sortBy) params.append('sortBy', sortBy);
  if (sortOrder) params.append('sortOrder', sortOrder);

  const queryString = params.toString();
  const url = `/api/jobs${queryString ? `?${queryString}` : ''}`;
  const response = await api.get(url);
  return response.data;
};

/**
 * Get a single job by ID.
 * @param {string} jobId - Job ID
 * @returns {Promise<Object>} Job details
 */
export const getJob = async (jobId) => {
  const response = await api.get(`/api/jobs/${encodeURIComponent(jobId)}`);
  return response.data;
};

/**
 * Get active swarm download jobs.
 * @returns {Promise<Array>} List of active swarm jobs
 */
export const getActiveSwarmJobs = async () => {
  try {
    const response = await api.get('/multisource/jobs');
    return Array.isArray(response.data?.jobs) ? response.data.jobs : [];
  } catch (error) {
    console.debug('Failed to fetch swarm jobs:', error);
    return [];
  }
};

/**
 * Get swarm job status by ID.
 * @param {string} jobId - Swarm job ID
 * @returns {Promise<Object|null>} Job status or null if not found
 */
export const getSwarmJobStatus = async (jobId) => {
  try {
    const response = await api.get(`/multisource/jobs/${encodeURIComponent(jobId)}`);
    return response.data;
  } catch (error) {
    if (error?.response?.status === 404) {
      return null;
    }
    throw error;
  }
};

/**
 * Get swarm trace summary with peer contributions.
 * @param {string} jobId - Swarm job ID
 * @returns {Promise<Object|null>} Trace summary or null if not found
 */
export const getSwarmTraceSummary = async (jobId) => {
  try {
    const response = await api.get(`/traces/${encodeURIComponent(jobId)}/summary`);
    return response.data;
  } catch (error) {
    if (error?.response?.status === 404) {
      return null;
    }
    console.debug('Failed to fetch swarm trace summary:', error);
    return null; // Trace may not be available for all jobs
  }
};
