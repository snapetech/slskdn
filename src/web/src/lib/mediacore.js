import { urlBase } from '../config';
import * as session from './session';

const baseUrl = `${urlBase}/api/v0/mediacore/contentid`;

/**
 * Register a mapping from external ID to ContentID.
 */
export const registerContentId = async (externalId, contentId) => {
  const response = await fetch(`${baseUrl}/register`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ externalId, contentId }),
  });

  if (!response.ok) {
    throw new Error(`Failed to register ContentID mapping: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Resolve an external ID to its ContentID.
 */
export const resolveContentId = async (externalId) => {
  const response = await fetch(`${baseUrl}/resolve/${encodeURIComponent(externalId)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    if (response.status === 404) {
      return null; // Not found
    }
    throw new Error(`Failed to resolve ContentID: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Check if an external ID is registered.
 */
export const checkContentIdExists = async (externalId) => {
  const response = await fetch(`${baseUrl}/exists/${encodeURIComponent(externalId)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to check ContentID existence: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get all external IDs mapped to a ContentID.
 */
export const getExternalIds = async (contentId) => {
  const response = await fetch(`${baseUrl}/external/${encodeURIComponent(contentId)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get external IDs: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get ContentID registry statistics.
 */
export const getContentIdStats = async () => {
  const response = await fetch(`${baseUrl}/stats`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get ContentID stats: ${response.statusText}`);
  }

  return response.json();
};
