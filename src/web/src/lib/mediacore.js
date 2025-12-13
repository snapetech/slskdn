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

/**
 * Find all ContentIDs for a specific domain.
 */
export const findContentIdsByDomain = async (domain) => {
  const response = await fetch(`${baseUrl}/domain/${encodeURIComponent(domain)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to find ContentIDs by domain: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Find all ContentIDs for a specific domain and type.
 */
export const findContentIdsByDomainAndType = async (domain, type) => {
  const response = await fetch(`${baseUrl}/domain/${encodeURIComponent(domain)}/type/${encodeURIComponent(type)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to find ContentIDs by domain and type: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Validate a ContentID format.
 */
export const validateContentId = async (contentId) => {
  const response = await fetch(`${baseUrl}/validate/${encodeURIComponent(contentId)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to validate ContentID: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Traverse the content graph following a specific link type.
 */
export const traverseContentGraph = async (startContentId, linkName, maxDepth = 3) => {
  const params = new URLSearchParams({ linkName, maxDepth: maxDepth.toString() });
  const response = await fetch(`${baseUrl.replace('contentid', 'ipld')}/traverse/${encodeURIComponent(startContentId)}?${params}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to traverse content graph: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get the content graph for a specific ContentID.
 */
export const getContentGraph = async (contentId, maxDepth = 2) => {
  const params = new URLSearchParams({ maxDepth: maxDepth.toString() });
  const response = await fetch(`${baseUrl.replace('contentid', 'ipld')}/graph/${encodeURIComponent(contentId)}?${params}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get content graph: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Find all content that links to the specified ContentID.
 */
export const findInboundLinks = async (targetContentId, linkName = null) => {
  const params = linkName ? new URLSearchParams({ linkName }) : '';
  const response = await fetch(`${baseUrl.replace('contentid', 'ipld')}/inbound/${encodeURIComponent(targetContentId)}${params ? '?' + params : ''}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to find inbound links: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Validate IPLD links in the registry.
 */
export const validateIpldLinks = async () => {
  const response = await fetch(`${baseUrl.replace('contentid', 'ipld')}/validate`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to validate IPLD links: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Add IPLD links to a content descriptor.
 */
export const addIpldLinks = async (contentId, links) => {
  const response = await fetch(`${baseUrl.replace('contentid', 'ipld')}/links/${encodeURIComponent(contentId)}`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ links }),
  });

  if (!response.ok) {
    throw new Error(`Failed to add IPLD links: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Compute perceptual hash for audio data.
 */
export const computeAudioHash = async (samples, sampleRate, algorithm = 'ChromaPrint') => {
  const response = await fetch(`${baseUrl.replace('contentid', 'perceptualhash')}/audio`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ samples, sampleRate, algorithm }),
  });

  if (!response.ok) {
    throw new Error(`Failed to compute audio hash: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Compute perceptual hash for image data.
 */
export const computeImageHash = async (pixels, width, height, algorithm = 'PHash') => {
  const response = await fetch(`${baseUrl.replace('contentid', 'perceptualhash')}/image`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ pixels, width, height, algorithm }),
  });

  if (!response.ok) {
    throw new Error(`Failed to compute image hash: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Compute similarity between two perceptual hashes.
 */
export const computeHashSimilarity = async (hashA, hashB, threshold = 0.8) => {
  const response = await fetch(`${baseUrl.replace('contentid', 'perceptualhash')}/similarity`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ hashA, hashB, threshold }),
  });

  if (!response.ok) {
    throw new Error(`Failed to compute hash similarity: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get supported perceptual hash algorithms.
 */
export const getSupportedHashAlgorithms = async () => {
  const response = await fetch(`${baseUrl.replace('contentid', 'perceptualhash')}/algorithms`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get hash algorithms: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Compute perceptual similarity between two ContentIDs.
 */
export const computePerceptualSimilarity = async (contentIdA, contentIdB, threshold = 0.7) => {
  const response = await fetch(`${baseUrl.replace('contentid', 'fuzzymatch')}/perceptual`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ contentIdA, contentIdB, threshold }),
  });

  if (!response.ok) {
    throw new Error(`Failed to compute perceptual similarity: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Find similar content for a given ContentID.
 */
export const findSimilarContent = async (contentId, options = {}) => {
  const response = await fetch(`${baseUrl.replace('contentid', 'fuzzymatch')}/find/${encodeURIComponent(contentId)}`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(options),
  });

  if (!response.ok) {
    throw new Error(`Failed to find similar content: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Compute text-based similarity between two strings.
 */
export const computeTextSimilarity = async (textA, textB) => {
  const response = await fetch(`${baseUrl.replace('contentid', 'fuzzymatch')}/text`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ textA, textB }),
  });

  if (!response.ok) {
    throw new Error(`Failed to compute text similarity: ${response.statusText}`);
  }

  return response.json();
};
