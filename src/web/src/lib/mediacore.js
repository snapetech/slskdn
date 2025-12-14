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

/**
 * Export metadata for specified ContentIDs.
 */
export const exportMetadata = async (contentIds, includeLinks = true) => {
  const response = await fetch(`${baseUrl.replace('contentid', 'portability')}/export`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ contentIds, includeLinks }),
  });

  if (!response.ok) {
    throw new Error(`Failed to export metadata: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Import metadata from a package.
 */
export const importMetadata = async (package, conflictStrategy = 'Merge', dryRun = false) => {
  const response = await fetch(`${baseUrl.replace('contentid', 'portability')}/import`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ package, conflictStrategy, dryRun }),
  });

  if (!response.ok) {
    throw new Error(`Failed to import metadata: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Analyze conflicts in a metadata package.
 */
export const analyzeMetadataConflicts = async (package) => {
  const response = await fetch(`${baseUrl.replace('contentid', 'portability')}/analyze`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ package }),
  });

  if (!response.ok) {
    throw new Error(`Failed to analyze conflicts: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get supported conflict resolution strategies.
 */
export const getConflictStrategies = async () => {
  const response = await fetch(`${baseUrl.replace('contentid', 'portability')}/strategies`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get conflict strategies: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get supported merge strategies.
 */
export const getMergeStrategies = async () => {
  const response = await fetch(`${baseUrl.replace('contentid', 'portability')}/merge-strategies`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get merge strategies: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Publish a content descriptor.
 */
export const publishContentDescriptor = async (descriptor, forceUpdate = false) => {
  const response = await fetch(`${baseUrl.replace('contentid', 'publish')}/descriptor`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ descriptor, forceUpdate }),
  });

  if (!response.ok) {
    throw new Error(`Failed to publish descriptor: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Publish multiple content descriptors in batch.
 */
export const publishContentDescriptorsBatch = async (descriptors) => {
  const response = await fetch(`${baseUrl.replace('contentid', 'publish')}/batch`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ descriptors }),
  });

  if (!response.ok) {
    throw new Error(`Failed to publish batch: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Update a published content descriptor.
 */
export const updateContentDescriptor = async (contentId, updates) => {
  const response = await fetch(`${baseUrl.replace('contentid', 'publish')}/descriptor/${encodeURIComponent(contentId)}`, {
    method: 'PUT',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ updates }),
  });

  if (!response.ok) {
    throw new Error(`Failed to update descriptor: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Republish descriptors that are about to expire.
 */
export const republishExpiringDescriptors = async (contentIds = null) => {
  const response = await fetch(`${baseUrl.replace('contentid', 'publish')}/republish`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ contentIds }),
  });

  if (!response.ok) {
    throw new Error(`Failed to republish descriptors: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Unpublish a content descriptor.
 */
export const unpublishContentDescriptor = async (contentId) => {
  const response = await fetch(`${baseUrl.replace('contentid', 'publish')}/descriptor/${encodeURIComponent(contentId)}`, {
    method: 'DELETE',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to unpublish descriptor: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get content descriptor publishing statistics.
 */
export const getPublishingStats = async () => {
  const response = await fetch(`${baseUrl.replace('contentid', 'publish')}/stats`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get publishing stats: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Retrieve a content descriptor by ContentID.
 */
export const retrieveContentDescriptor = async (contentId, bypassCache = false) => {
  const params = bypassCache ? new URLSearchParams({ bypassCache: 'true' }) : '';
  const response = await fetch(`${baseUrl.replace('contentid', 'retrieve')}/descriptor/${encodeURIComponent(contentId)}${params ? '?' + params : ''}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    if (response.status === 404) {
      return { found: false, contentId };
    }
    throw new Error(`Failed to retrieve descriptor: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Retrieve multiple content descriptors in batch.
 */
export const retrieveContentDescriptorsBatch = async (contentIds) => {
  const response = await fetch(`${baseUrl.replace('contentid', 'retrieve')}/batch`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ contentIds }),
  });

  if (!response.ok) {
    throw new Error(`Failed to retrieve batch: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Query descriptors by domain and type.
 */
export const queryDescriptorsByDomain = async (domain, type = null, maxResults = 50) => {
  const params = new URLSearchParams({ maxResults: maxResults.toString() });
  if (type) params.set('type', type);

  const response = await fetch(`${baseUrl.replace('contentid', 'retrieve')}/query/domain/${encodeURIComponent(domain)}?${params}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to query domain: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Verify a content descriptor's signature and freshness.
 */
export const verifyContentDescriptor = async (descriptor, retrievedAt = null) => {
  const response = await fetch(`${baseUrl.replace('contentid', 'retrieve')}/verify`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ descriptor, retrievedAt }),
  });

  if (!response.ok) {
    throw new Error(`Failed to verify descriptor: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get descriptor retrieval statistics.
 */
export const getRetrievalStats = async () => {
  const response = await fetch(`${baseUrl.replace('contentid', 'retrieve')}/stats`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get retrieval stats: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Clear the descriptor retrieval cache.
 */
export const clearRetrievalCache = async () => {
  const response = await fetch(`${baseUrl.replace('contentid', 'retrieve')}/cache/clear`, {
    method: 'POST',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to clear cache: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get MediaCore statistics dashboard.
 */
export const getMediaCoreDashboard = async () => {
  const response = await fetch(`${baseUrl.replace('contentid', 'stats')}/dashboard`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get dashboard: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get content registry statistics.
 */
export const getContentRegistryStats = async () => {
  const response = await fetch(`${baseUrl.replace('contentid', 'stats')}/registry`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get registry stats: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get descriptor statistics.
 */
export const getDescriptorStats = async () => {
  const response = await fetch(`${baseUrl.replace('contentid', 'stats')}/descriptors`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get descriptor stats: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get fuzzy matching statistics.
 */
export const getFuzzyMatchingStats = async () => {
  const response = await fetch(`${baseUrl.replace('contentid', 'stats')}/fuzzy`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get fuzzy matching stats: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get IPLD mapping statistics.
 */
export const getIpldMappingStats = async () => {
  const response = await fetch(`${baseUrl.replace('contentid', 'stats')}/ipld`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get IPLD mapping stats: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get perceptual hashing statistics.
 */
export const getPerceptualHashingStats = async () => {
  const response = await fetch(`${baseUrl.replace('contentid', 'stats')}/perceptual`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get perceptual hashing stats: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get metadata portability statistics.
 */
export const getMetadataPortabilityStats = async () => {
  const response = await fetch(`${baseUrl.replace('contentid', 'stats')}/portability`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get portability stats: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get content publishing statistics.
 */
export const getContentPublishingStats = async () => {
  const response = await fetch(`${baseUrl.replace('contentid', 'stats')}/publishing`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get publishing stats: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Reset all MediaCore statistics.
 */
export const resetMediaCoreStats = async () => {
  const response = await fetch(`${baseUrl.replace('contentid', 'stats')}/reset`, {
    method: 'POST',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to reset stats: ${response.statusText}`);
  }

  return response.json();
};

// PodCore DHT Publishing API functions
const podBaseUrl = baseUrl.replace('contentid', 'podcore/dht');

/**
 * Publish pod metadata to DHT.
 */
export const publishPod = async (pod) => {
  const response = await fetch(`${podBaseUrl}/publish`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ pod }),
  });

  if (!response.ok) {
    throw new Error(`Failed to publish pod: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Update existing pod metadata in DHT.
 */
export const updatePod = async (pod) => {
  const response = await fetch(`${podBaseUrl}/update`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ pod }),
  });

  if (!response.ok) {
    throw new Error(`Failed to update pod: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Unpublish pod metadata from DHT.
 */
export const unpublishPod = async (podId) => {
  const response = await fetch(`${podBaseUrl}/unpublish/${encodeURIComponent(podId)}`, {
    method: 'DELETE',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to unpublish pod: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get published pod metadata from DHT.
 */
export const getPublishedPodMetadata = async (podId) => {
  const response = await fetch(`${podBaseUrl}/metadata/${encodeURIComponent(podId)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get pod metadata: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Refresh published pod metadata.
 */
export const refreshPod = async (podId) => {
  const response = await fetch(`${podBaseUrl}/refresh/${encodeURIComponent(podId)}`, {
    method: 'POST',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to refresh pod: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get pod publishing statistics.
 */
export const getPodPublishingStats = async () => {
  const response = await fetch(`${podBaseUrl}/stats`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get publishing stats: ${response.statusText}`);
  }

  return response.json();
};

// Pod Membership API functions
const membershipBaseUrl = baseUrl.replace('contentid', 'podcore/membership');

/**
 * Publish membership record to DHT.
 */
export const publishMembership = async (membershipRecord) => {
  const response = await fetch(`${membershipBaseUrl}/publish`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(membershipRecord),
  });

  if (!response.ok) {
    throw new Error(`Failed to publish membership: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Update membership record in DHT.
 */
export const updateMembership = async (membershipRecord) => {
  const response = await fetch(`${membershipBaseUrl}/update`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(membershipRecord),
  });

  if (!response.ok) {
    throw new Error(`Failed to update membership: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Remove membership record from DHT.
 */
export const removeMembership = async (podId, peerId) => {
  const response = await fetch(`${membershipBaseUrl}/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}`, {
    method: 'DELETE',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to remove membership: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get membership record from DHT.
 */
export const getMembership = async (podId, peerId) => {
  const response = await fetch(`${membershipBaseUrl}/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get membership: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Verify membership in a pod.
 */
export const verifyMembership = async (podId, peerId) => {
  const response = await fetch(`${membershipBaseUrl}/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}/verify`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to verify membership: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Ban a member from a pod.
 */
export const banMember = async (podId, peerId, reason) => {
  const response = await fetch(`${membershipBaseUrl}/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}/ban`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ reason }),
  });

  if (!response.ok) {
    throw new Error(`Failed to ban member: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Unban a member from a pod.
 */
export const unbanMember = async (podId, peerId) => {
  const response = await fetch(`${membershipBaseUrl}/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}/unban`, {
    method: 'POST',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to unban member: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Change a member's role in a pod.
 */
export const changeMemberRole = async (podId, peerId, newRole) => {
  const response = await fetch(`${membershipBaseUrl}/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}/role`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ newRole }),
  });

  if (!response.ok) {
    throw new Error(`Failed to change role: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get membership statistics.
 */
export const getMembershipStats = async () => {
  const response = await fetch(`${membershipBaseUrl}/stats`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get membership stats: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Clean up expired membership records.
 */
export const cleanupExpiredMemberships = async () => {
  const response = await fetch(`${membershipBaseUrl}/cleanup`, {
    method: 'POST',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to cleanup memberships: ${response.statusText}`);
  }

  return response.json();
};

// Pod Discovery API functions
const discoveryBaseUrl = baseUrl.replace('contentid', 'podcore/discovery');

/**
 * Register a pod for discovery.
 */
export const registerPodForDiscovery = async (pod) => {
  const response = await fetch(`${discoveryBaseUrl}/register`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(pod),
  });

  if (!response.ok) {
    throw new Error(`Failed to register pod: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Unregister a pod from discovery.
 */
export const unregisterPodFromDiscovery = async (podId) => {
  const response = await fetch(`${discoveryBaseUrl}/unregister/${encodeURIComponent(podId)}`, {
    method: 'DELETE',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to unregister pod: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Update pod discovery information.
 */
export const updatePodDiscovery = async (pod) => {
  const response = await fetch(`${discoveryBaseUrl}/update`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(pod),
  });

  if (!response.ok) {
    throw new Error(`Failed to update pod discovery: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Discover pods by name.
 */
export const discoverPodsByName = async (name) => {
  const response = await fetch(`${discoveryBaseUrl}/name/${encodeURIComponent(name)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to discover pods by name: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Discover pods by tag.
 */
export const discoverPodsByTag = async (tag) => {
  const response = await fetch(`${discoveryBaseUrl}/tag/${encodeURIComponent(tag)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to discover pods by tag: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Discover pods by multiple tags.
 */
export const discoverPodsByTags = async (tags) => {
  const tagsParam = tags.join(',');
  const response = await fetch(`${discoveryBaseUrl}/tags/${encodeURIComponent(tagsParam)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to discover pods by tags: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Discover all pods.
 */
export const discoverAllPods = async (limit = 50) => {
  const response = await fetch(`${discoveryBaseUrl}/all?limit=${limit}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to discover all pods: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Discover pods by content ID.
 */
export const discoverPodsByContent = async (contentId) => {
  const response = await fetch(`${discoveryBaseUrl}/content/${encodeURIComponent(contentId)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to discover pods by content: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get pod discovery statistics.
 */
export const getPodDiscoveryStats = async () => {
  const response = await fetch(`${discoveryBaseUrl}/stats`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get discovery stats: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Refresh pod discovery entries.
 */
export const refreshPodDiscovery = async () => {
  const response = await fetch(`${discoveryBaseUrl}/refresh`, {
    method: 'POST',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to refresh discovery: ${response.statusText}`);
  }

  return response.json();
};

// Pod Join/Leave API functions
const membershipBaseUrl = baseUrl.replace('contentid', 'podcore/membership');

/**
 * Submit a signed join request to a pod.
 */
export const requestPodJoin = async (joinRequest) => {
  const response = await fetch(`${membershipBaseUrl}/join`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(joinRequest),
  });

  if (!response.ok) {
    throw new Error(`Failed to request join: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Accept or reject a join request.
 */
export const acceptPodJoin = async (acceptance) => {
  const response = await fetch(`${membershipBaseUrl}/join/accept`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(acceptance),
  });

  if (!response.ok) {
    throw new Error(`Failed to accept join: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Submit a signed leave request from a pod.
 */
export const requestPodLeave = async (leaveRequest) => {
  const response = await fetch(`${membershipBaseUrl}/leave`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(leaveRequest),
  });

  if (!response.ok) {
    throw new Error(`Failed to request leave: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Accept a leave request.
 */
export const acceptPodLeave = async (acceptance) => {
  const response = await fetch(`${membershipBaseUrl}/leave/accept`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(acceptance),
  });

  if (!response.ok) {
    throw new Error(`Failed to accept leave: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get pending join requests for a pod.
 */
export const getPendingJoinRequests = async (podId) => {
  const response = await fetch(`${membershipBaseUrl}/join/pending/${encodeURIComponent(podId)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get pending join requests: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get pending leave requests for a pod.
 */
export const getPendingLeaveRequests = async (podId) => {
  const response = await fetch(`${membershipBaseUrl}/leave/pending/${encodeURIComponent(podId)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get pending leave requests: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Cancel a pending join request.
 */
export const cancelJoinRequest = async (podId, peerId) => {
  const response = await fetch(`${membershipBaseUrl}/join/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}`, {
    method: 'DELETE',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to cancel join request: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Cancel a pending leave request.
 */
export const cancelLeaveRequest = async (podId, peerId) => {
  const response = await fetch(`${membershipBaseUrl}/leave/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}`, {
    method: 'DELETE',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to cancel leave request: ${response.statusText}`);
  }

  return response.json();
};

// Pod Message Routing API functions
const routingBaseUrl = baseUrl.replace('contentid', 'podcore/routing');

/**
 * Manually route a pod message through the overlay network.
 */
export const routePodMessage = async (message) => {
  const response = await fetch(`${routingBaseUrl}/route`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(message),
  });

  if (!response.ok) {
    throw new Error(`Failed to route message: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Route a pod message to specific peers.
 */
export const routePodMessageToPeers = async (message, targetPeerIds) => {
  const response = await fetch(`${routingBaseUrl}/route-to-peers`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ message, targetPeerIds }),
  });

  if (!response.ok) {
    throw new Error(`Failed to route message to peers: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get pod message routing statistics.
 */
export const getPodMessageRoutingStats = async () => {
  const response = await fetch(`${routingBaseUrl}/stats`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get routing stats: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Check if a message has been seen for deduplication.
 */
export const checkMessageSeen = async (messageId, podId) => {
  const response = await fetch(`${routingBaseUrl}/seen/${encodeURIComponent(messageId)}/${encodeURIComponent(podId)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to check message seen status: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Register a message as seen for deduplication.
 */
export const registerMessageSeen = async (messageId, podId) => {
  const response = await fetch(`${routingBaseUrl}/seen/${encodeURIComponent(messageId)}/${encodeURIComponent(podId)}`, {
    method: 'POST',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to register message as seen: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Clean up old seen message entries.
 */
export const cleanupSeenMessages = async () => {
  const response = await fetch(`${routingBaseUrl}/cleanup`, {
    method: 'POST',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to cleanup seen messages: ${response.statusText}`);
  }

  return response.json();
};

// Pod Message Signing API functions
const signingBaseUrl = baseUrl.replace('contentid', 'podcore/signing');

/**
 * Sign a pod message.
 */
export const signPodMessage = async (message, privateKey) => {
  const response = await fetch(`${signingBaseUrl}/sign`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ message, privateKey }),
  });

  if (!response.ok) {
    throw new Error(`Failed to sign message: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Verify a pod message signature.
 */
export const verifyPodMessageSignature = async (message) => {
  const response = await fetch(`${signingBaseUrl}/verify`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(message),
  });

  if (!response.ok) {
    throw new Error(`Failed to verify message signature: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Generate a new key pair for message signing.
 */
export const generateMessageKeyPair = async () => {
  const response = await fetch(`${signingBaseUrl}/generate-keypair`, {
    method: 'POST',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to generate key pair: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get message signing statistics.
 */
export const getMessageSigningStats = async () => {
  const response = await fetch(`${signingBaseUrl}/stats`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get signing stats: ${response.statusText}`);
  }

  return response.json();
};

// Pod Membership Verification API functions
const verificationBaseUrl = baseUrl.replace('contentid', 'podcore/verification');

/**
 * Verify membership in a pod.
 */
export const verifyPodMembership = async (podId, peerId) => {
  const response = await fetch(`${verificationBaseUrl}/membership/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to verify membership: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Verify a pod message authenticity.
 */
export const verifyPodMessage = async (message) => {
  const response = await fetch(`${verificationBaseUrl}/message`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(message),
  });

  if (!response.ok) {
    throw new Error(`Failed to verify message: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Check if a peer has a required role in a pod.
 */
export const checkPodRole = async (podId, peerId, requiredRole) => {
  const response = await fetch(`${verificationBaseUrl}/role/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}/${encodeURIComponent(requiredRole)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to check role: ${response.statusText}`);
  }

  const result = await response.json();
  return result.hasRole; // Assuming API returns { hasRole: boolean }
};

/**
 * Get membership verification statistics.
 */
export const getVerificationStats = async () => {
  const response = await fetch(`${verificationBaseUrl}/stats`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get verification stats: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Message Storage API base URL
 */
const storageBaseUrl = `${apiBaseUrl}/pods/messages`;

/**
 * Search messages in a pod.
 */
export const searchMessages = async (podId, query, channelId = null, limit = 50) => {
  const params = new URLSearchParams({ query });
  if (channelId) params.append('channelId', channelId);
  if (limit) params.append('limit', limit);

  const response = await fetch(`${storageBaseUrl}/${podId}/search?${params}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to search messages: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get message storage statistics.
 */
export const getMessageStorageStats = async () => {
  const response = await fetch(`${storageBaseUrl}/stats`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get message storage stats: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Clean up messages older than the specified timestamp.
 */
export const cleanupMessages = async (olderThan) => {
  const response = await fetch(`${storageBaseUrl}/cleanup?olderThan=${olderThan}`, {
    method: 'DELETE',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to cleanup messages: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Clean up messages in a specific channel older than the specified timestamp.
 */
export const cleanupChannelMessages = async (podId, channelId, olderThan) => {
  const response = await fetch(`${storageBaseUrl}/${podId}/${channelId}/cleanup?olderThan=${olderThan}`, {
    method: 'DELETE',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to cleanup channel messages: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get message count for a pod and channel.
 */
export const getMessageCount = async (podId, channelId) => {
  const response = await fetch(`${storageBaseUrl}/${podId}/${channelId}/count`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get message count: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Rebuild the full-text search index.
 */
export const rebuildSearchIndex = async () => {
  const response = await fetch(`${storageBaseUrl}/rebuild-index`, {
    method: 'POST',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to rebuild search index: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Vacuum the message storage database.
 */
export const vacuumDatabase = async () => {
  const response = await fetch(`${storageBaseUrl}/vacuum`, {
    method: 'POST',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to vacuum database: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Message Backfill API base URL
 */
const backfillBaseUrl = `${apiBaseUrl}/pods/backfill`;

/**
 * Sync backfill for a pod on rejoin.
 */
export const syncPodBackfill = async (podId, lastSeenTimestamps) => {
  const response = await fetch(`${backfillBaseUrl}/${podId}/sync`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(lastSeenTimestamps),
  });

  if (!response.ok) {
    throw new Error(`Failed to sync pod backfill: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get last seen timestamps for a pod.
 */
export const getLastSeenTimestamps = async (podId) => {
  const response = await fetch(`${backfillBaseUrl}/${podId}/last-seen`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get last seen timestamps: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Update last seen timestamp for a channel.
 */
export const updateLastSeenTimestamp = async (podId, channelId, timestamp) => {
  const response = await fetch(`${backfillBaseUrl}/${podId}/${channelId}/last-seen`, {
    method: 'PUT',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(timestamp),
  });

  if (!response.ok) {
    throw new Error(`Failed to update last seen timestamp: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get backfill statistics.
 */
export const getBackfillStats = async () => {
  const response = await fetch(`${backfillBaseUrl}/stats`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get backfill stats: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Sync backfill for all pods.
 */
export const syncAllPodsBackfill = async () => {
  const response = await fetch(`${backfillBaseUrl}/sync-all`, {
    method: 'POST',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to sync all pods backfill: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Pod Opinion API base URL
 */
const opinionBaseUrl = `${apiBaseUrl}/pods`;

/**
 * Publish an opinion on a content variant.
 */
export const publishOpinion = async (podId, opinion) => {
  const response = await fetch(`${opinionBaseUrl}/${podId}/opinions`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(opinion),
  });

  if (!response.ok) {
    throw new Error(`Failed to publish opinion: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get all opinions for a content item.
 */
export const getContentOpinions = async (podId, contentId) => {
  const response = await fetch(`${opinionBaseUrl}/${podId}/opinions/content/${encodeURIComponent(contentId)}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get content opinions: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get opinions for a specific variant.
 */
export const getVariantOpinions = async (podId, contentId, variantHash) => {
  const response = await fetch(`${opinionBaseUrl}/${podId}/opinions/content/${encodeURIComponent(contentId)}/variant/${variantHash}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get variant opinions: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get opinion statistics for a content item.
 */
export const getOpinionStatistics = async (podId, contentId) => {
  const response = await fetch(`${opinionBaseUrl}/${podId}/opinions/content/${encodeURIComponent(contentId)}/stats`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get opinion statistics: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Refresh opinions for a pod from DHT.
 */
export const refreshPodOpinions = async (podId) => {
  const response = await fetch(`${opinionBaseUrl}/${podId}/opinions/refresh`, {
    method: 'POST',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to refresh pod opinions: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get aggregated opinions with affinity weighting.
 */
export const getAggregatedOpinions = async (podId, contentId) => {
  const response = await fetch(`${opinionBaseUrl}/${podId}/opinions/content/${encodeURIComponent(contentId)}/aggregated`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get aggregated opinions: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get member affinity scores.
 */
export const getMemberAffinities = async (podId) => {
  const response = await fetch(`${opinionBaseUrl}/${podId}/opinions/members/affinity`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get member affinities: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get consensus recommendations for content variants.
 */
export const getConsensusRecommendations = async (podId, contentId) => {
  const response = await fetch(`${opinionBaseUrl}/${podId}/opinions/content/${encodeURIComponent(contentId)}/recommendations`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get consensus recommendations: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Update member affinity scores.
 */
export const updateMemberAffinities = async (podId) => {
  const response = await fetch(`${opinionBaseUrl}/${podId}/opinions/members/affinity/update`, {
    method: 'POST',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to update member affinities: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Pod Channel API base URL
 */
const channelBaseUrl = `${apiBaseUrl}/pods`;

/**
 * Create a new channel in a pod.
 */
export const createChannel = async (podId, channel) => {
  const response = await fetch(`${channelBaseUrl}/${podId}/channels`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(channel),
  });

  if (!response.ok) {
    throw new Error(`Failed to create channel: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get all channels in a pod.
 */
export const getChannels = async (podId) => {
  const response = await fetch(`${channelBaseUrl}/${podId}/channels`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get channels: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get a specific channel in a pod.
 */
export const getChannel = async (podId, channelId) => {
  const response = await fetch(`${channelBaseUrl}/${podId}/channels/${channelId}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get channel: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Update a channel in a pod.
 */
export const updateChannel = async (podId, channelId, channel) => {
  const response = await fetch(`${channelBaseUrl}/${podId}/channels/${channelId}`, {
    method: 'PUT',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(channel),
  });

  if (!response.ok) {
    throw new Error(`Failed to update channel: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Delete a channel from a pod.
 */
export const deleteChannel = async (podId, channelId) => {
  const response = await fetch(`${channelBaseUrl}/${podId}/channels/${channelId}`, {
    method: 'DELETE',
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to delete channel: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Content API base URL
 */
const contentBaseUrl = `${apiBaseUrl}/pods/content`;

/**
 * Validate a content ID for pod linking.
 */
export const validateContentId = async (contentId) => {
  const response = await fetch(`${contentBaseUrl}/validate`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(contentId),
  });

  if (!response.ok) {
    throw new Error(`Failed to validate content ID: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Get metadata for a content ID.
 */
export const getContentMetadata = async (contentId) => {
  const params = new URLSearchParams({ contentId });
  const response = await fetch(`${contentBaseUrl}/metadata?${params}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to get content metadata: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Search for content that can be linked to pods.
 */
export const searchContent = async (query, domain = null, limit = 20) => {
  const params = new URLSearchParams({ query });
  if (domain) params.append('domain', domain);
  if (limit) params.append('limit', limit);

  const response = await fetch(`${contentBaseUrl}/search?${params}`, {
    headers: session.authHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Failed to search content: ${response.statusText}`);
  }

  return response.json();
};

/**
 * Create a pod linked to specific content.
 */
export const createContentLinkedPod = async (podRequest) => {
  const response = await fetch(`${contentBaseUrl}/create-pod`, {
    method: 'POST',
    headers: {
      ...session.authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(podRequest),
  });

  if (!response.ok) {
    throw new Error(`Failed to create content-linked pod: ${response.statusText}`);
  }

  return response.json();
};
