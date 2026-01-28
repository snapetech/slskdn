import { apiBaseUrl } from '../config';
import api from './api';

const baseUrl = '/api/v0/mediacore/contentid';
const ipldBaseUrl = '/api/v0/mediacore/ipld';
const perceptualHashBaseUrl = '/api/v0/mediacore/perceptualhash';
const fuzzyMatchBaseUrl = '/api/v0/mediacore/fuzzymatch';
const portabilityBaseUrl = '/api/v0/mediacore/portability';
const publishBaseUrl = '/api/v0/mediacore/publish';
const retrieveBaseUrl = '/api/v0/mediacore/retrieve';
const statsBaseUrl = '/api/v0/mediacore/stats';
const podDhtBaseUrl = '/api/v0/mediacore/podcore/dht';
const podMembershipBaseUrl = '/api/v0/mediacore/podcore/membership';
const podDiscoveryBaseUrl = '/api/v0/mediacore/podcore/discovery';
const podRoutingBaseUrl = '/api/v0/mediacore/podcore/routing';
const podSigningBaseUrl = '/api/v0/mediacore/podcore/signing';
const podVerificationBaseUrl = '/api/v0/mediacore/podcore/verification';

/**
 * Register a mapping from external ID to ContentID.
 */
export const registerContentId = async (externalId, contentId) => {
  return (await api.post(`${baseUrl}/register`, { contentId, externalId }))
    .data;
};

/**
 * Resolve an external ID to its ContentID.
 */
export const resolveContentId = async (externalId) => {
  try {
    return (
      await api.get(`${baseUrl}/resolve/${encodeURIComponent(externalId)}`)
    ).data;
  } catch (error) {
    if (error.response?.status === 404) {
      return null; // Not found
    }

    throw error;
  }
};

/**
 * Check if an external ID is registered.
 */
export const checkContentIdExists = async (externalId) => {
  return (await api.get(`${baseUrl}/exists/${encodeURIComponent(externalId)}`))
    .data;
};

/**
 * Get all external IDs mapped to a ContentID.
 */
export const getExternalIds = async (contentId) => {
  return (await api.get(`${baseUrl}/external/${encodeURIComponent(contentId)}`))
    .data;
};

/**
 * Get ContentID registry statistics.
 */
export const getContentIdStats = async () => {
  return (await api.get(`${baseUrl}/stats`)).data;
};

/**
 * Find all ContentIDs for a specific domain.
 */
export const findContentIdsByDomain = async (domain) => {
  return (await api.get(`${baseUrl}/domain/${encodeURIComponent(domain)}`))
    .data;
};

/**
 * Find all ContentIDs for a specific domain and type.
 */
export const findContentIdsByDomainAndType = async (domain, type) => {
  return (
    await api.get(
      `${baseUrl}/domain/${encodeURIComponent(domain)}/type/${encodeURIComponent(type)}`,
    )
  ).data;
};

/**
 * Validate a ContentID format.
 */
export const validateContentId = async (contentId) => {
  return (await api.get(`${baseUrl}/validate/${encodeURIComponent(contentId)}`))
    .data;
};

/**
 * Traverse the content graph following a specific link type.
 */
export const traverseContentGraph = async (
  startContentId,
  linkName,
  maxDepth = 3,
) => {
  return (
    await api.get(
      `${ipldBaseUrl}/traverse/${encodeURIComponent(startContentId)}`,
      {
        params: { linkName, maxDepth },
      },
    )
  ).data;
};

/**
 * Get the content graph for a specific ContentID.
 */
export const getContentGraph = async (contentId, maxDepth = 2) => {
  return (
    await api.get(`${ipldBaseUrl}/graph/${encodeURIComponent(contentId)}`, {
      params: { maxDepth },
    })
  ).data;
};

/**
 * Find all content that links to the specified ContentID.
 */
export const findInboundLinks = async (targetContentId, linkName = null) => {
  const parameters = linkName ? { linkName } : {};
  return (
    await api.get(
      `${ipldBaseUrl}/inbound/${encodeURIComponent(targetContentId)}`,
      { params: parameters },
    )
  ).data;
};

/**
 * Validate IPLD links in the registry.
 */
export const validateIpldLinks = async () => {
  return (await api.get(`${ipldBaseUrl}/validate`)).data;
};

/**
 * Add IPLD links to a content descriptor.
 */
export const addIpldLinks = async (contentId, links) => {
  return (
    await api.post(`${ipldBaseUrl}/links/${encodeURIComponent(contentId)}`, {
      links,
    })
  ).data;
};

/**
 * Compute perceptual hash for audio data.
 */
export const computeAudioHash = async (
  samples,
  sampleRate,
  algorithm = 'ChromaPrint',
) => {
  return (
    await api.post(`${perceptualHashBaseUrl}/audio`, {
      algorithm,
      sampleRate,
      samples,
    })
  ).data;
};

/**
 * Compute perceptual hash for image data.
 */
export const computeImageHash = async (
  pixels,
  width,
  height,
  algorithm = 'PHash',
) => {
  return (
    await api.post(`${perceptualHashBaseUrl}/image`, {
      algorithm,
      height,
      pixels,
      width,
    })
  ).data;
};

/**
 * Compute similarity between two perceptual hashes.
 */
export const computeHashSimilarity = async (hashA, hashB, threshold = 0.8) => {
  return (
    await api.post(`${perceptualHashBaseUrl}/similarity`, {
      hashA,
      hashB,
      threshold,
    })
  ).data;
};

/**
 * Get supported perceptual hash algorithms.
 */
export const getSupportedHashAlgorithms = async () => {
  return (await api.get(`${perceptualHashBaseUrl}/algorithms`)).data;
};

/**
 * Compute perceptual similarity between two ContentIDs.
 */
export const computePerceptualSimilarity = async (
  contentIdA,
  contentIdB,
  threshold = 0.7,
) => {
  return (
    await api.post(`${fuzzyMatchBaseUrl}/perceptual`, {
      contentIdA,
      contentIdB,
      threshold,
    })
  ).data;
};

/**
 * Find similar content for a given ContentID.
 */
export const findSimilarContent = async (contentId, options = {}) => {
  return (
    await api.post(
      `${fuzzyMatchBaseUrl}/find/${encodeURIComponent(contentId)}`,
      options,
    )
  ).data;
};

/**
 * Compute text-based similarity between two strings.
 */
export const computeTextSimilarity = async (textA, textB) => {
  return (await api.post(`${fuzzyMatchBaseUrl}/text`, { textA, textB })).data;
};

/**
 * Export metadata for specified ContentIDs.
 */
export const exportMetadata = async (contentIds, includeLinks = true) => {
  return (
    await api.post(`${portabilityBaseUrl}/export`, { contentIds, includeLinks })
  ).data;
};

/**
 * Import metadata from a package.
 */
export const importMetadata = async (
  packageData,
  conflictStrategy = 'Merge',
  dryRun = false,
) => {
  return (
    await api.post(`${portabilityBaseUrl}/import`, {
      conflictStrategy,
      dryRun,
      package: packageData,
    })
  ).data;
};

/**
 * Analyze conflicts in a metadata package.
 */
export const analyzeMetadataConflicts = async (packageData) => {
  return (
    await api.post(`${portabilityBaseUrl}/analyze`, { package: packageData })
  ).data;
};

/**
 * Get supported conflict resolution strategies.
 */
export const getConflictStrategies = async () => {
  return (await api.get(`${portabilityBaseUrl}/strategies`)).data;
};

/**
 * Get supported merge strategies.
 */
export const getMergeStrategies = async () => {
  return (await api.get(`${portabilityBaseUrl}/merge-strategies`)).data;
};

/**
 * Publish a content descriptor.
 */
export const publishContentDescriptor = async (
  descriptor,
  forceUpdate = false,
) => {
  return (
    await api.post(`${publishBaseUrl}/descriptor`, { descriptor, forceUpdate })
  ).data;
};

/**
 * Publish multiple content descriptors in batch.
 */
export const publishContentDescriptorsBatch = async (descriptors) => {
  return (await api.post(`${publishBaseUrl}/batch`, { descriptors })).data;
};

/**
 * Update a published content descriptor.
 */
export const updateContentDescriptor = async (contentId, updates) => {
  return (
    await api.put(
      `${publishBaseUrl}/descriptor/${encodeURIComponent(contentId)}`,
      { updates },
    )
  ).data;
};

/**
 * Republish descriptors that are about to expire.
 */
export const republishExpiringDescriptors = async (contentIds = null) => {
  return (await api.post(`${publishBaseUrl}/republish`, { contentIds })).data;
};

/**
 * Unpublish a content descriptor.
 */
export const unpublishContentDescriptor = async (contentId) => {
  return (
    await api.delete(
      `${publishBaseUrl}/descriptor/${encodeURIComponent(contentId)}`,
    )
  ).data;
};

/**
 * Get content descriptor publishing statistics.
 */
export const getPublishingStats = async () => {
  return (await api.get(`${publishBaseUrl}/stats`)).data;
};

/**
 * Retrieve a content descriptor by ContentID.
 */
export const retrieveContentDescriptor = async (
  contentId,
  bypassCache = false,
) => {
  try {
    return (
      await api.get(
        `${retrieveBaseUrl}/descriptor/${encodeURIComponent(contentId)}`,
        {
          params: bypassCache ? { bypassCache: 'true' } : {},
        },
      )
    ).data;
  } catch (error) {
    if (error.response?.status === 404) {
      return { contentId, found: false };
    }

    throw error;
  }
};

/**
 * Retrieve multiple content descriptors in batch.
 */
export const retrieveContentDescriptorsBatch = async (contentIds) => {
  return (await api.post(`${retrieveBaseUrl}/batch`, { contentIds })).data;
};

/**
 * Query descriptors by domain and type.
 */
export const queryDescriptorsByDomain = async (
  domain,
  type = null,
  maxResults = 50,
) => {
  const parameters = { maxResults };
  if (type) parameters.type = type;
  return (
    await api.get(
      `${retrieveBaseUrl}/query/domain/${encodeURIComponent(domain)}`,
      { params: parameters },
    )
  ).data;
};

/**
 * Verify a content descriptor's signature and freshness.
 */
export const verifyContentDescriptor = async (
  descriptor,
  retrievedAt = null,
) => {
  return (
    await api.post(`${retrieveBaseUrl}/verify`, { descriptor, retrievedAt })
  ).data;
};

/**
 * Get descriptor retrieval statistics.
 */
export const getRetrievalStats = async () => {
  return (await api.get(`${retrieveBaseUrl}/stats`)).data;
};

/**
 * Clear the descriptor retrieval cache.
 */
export const clearRetrievalCache = async () => {
  return (await api.post(`${retrieveBaseUrl}/cache/clear`)).data;
};

/**
 * Get MediaCore statistics dashboard.
 */
export const getMediaCoreDashboard = async () => {
  return (await api.get(`${statsBaseUrl}/dashboard`)).data;
};

/**
 * Get content registry statistics.
 */
export const getContentRegistryStats = async () => {
  return (await api.get(`${statsBaseUrl}/registry`)).data;
};

/**
 * Get descriptor statistics.
 */
export const getDescriptorStats = async () => {
  return (await api.get(`${statsBaseUrl}/descriptors`)).data;
};

/**
 * Get fuzzy matching statistics.
 */
export const getFuzzyMatchingStats = async () => {
  return (await api.get(`${statsBaseUrl}/fuzzy`)).data;
};

/**
 * Get IPLD mapping statistics.
 */
export const getIpldMappingStats = async () => {
  return (await api.get(`${statsBaseUrl}/ipld`)).data;
};

/**
 * Get perceptual hashing statistics.
 */
export const getPerceptualHashingStats = async () => {
  return (await api.get(`${statsBaseUrl}/perceptual`)).data;
};

/**
 * Get metadata portability statistics.
 */
export const getMetadataPortabilityStats = async () => {
  return (await api.get(`${statsBaseUrl}/portability`)).data;
};

/**
 * Get content publishing statistics.
 */
export const getContentPublishingStats = async () => {
  return (await api.get(`${statsBaseUrl}/publishing`)).data;
};

/**
 * Reset all MediaCore statistics.
 */
export const resetMediaCoreStats = async () => {
  return (await api.post(`${statsBaseUrl}/reset`)).data;
};

// PodCore DHT Publishing API functions

/**
 * Publish pod metadata to DHT.
 */
export const publishPod = async (pod) => {
  return (await api.post(`${podDhtBaseUrl}/publish`, { pod })).data;
};

/**
 * Update existing pod metadata in DHT.
 */
export const updatePod = async (pod) => {
  return (await api.post(`${podDhtBaseUrl}/update`, { pod })).data;
};

/**
 * Unpublish pod metadata from DHT.
 */
export const unpublishPod = async (podId) => {
  return (
    await api.delete(`${podDhtBaseUrl}/unpublish/${encodeURIComponent(podId)}`)
  ).data;
};

/**
 * Get published pod metadata from DHT.
 */
export const getPublishedPodMetadata = async (podId) => {
  return (
    await api.get(`${podDhtBaseUrl}/metadata/${encodeURIComponent(podId)}`)
  ).data;
};

/**
 * Refresh published pod metadata.
 */
export const refreshPod = async (podId) => {
  return (
    await api.post(`${podDhtBaseUrl}/refresh/${encodeURIComponent(podId)}`)
  ).data;
};

/**
 * Get pod publishing statistics.
 */
export const getPodPublishingStats = async () => {
  return (await api.get(`${podDhtBaseUrl}/stats`)).data;
};

// Pod Membership API functions

/**
 * Publish membership record to DHT.
 */
export const publishMembership = async (membershipRecord) => {
  return (await api.post(`${podMembershipBaseUrl}/publish`, membershipRecord))
    .data;
};

/**
 * Update membership record in DHT.
 */
export const updateMembership = async (membershipRecord) => {
  return (await api.post(`${podMembershipBaseUrl}/update`, membershipRecord))
    .data;
};

/**
 * Remove membership record from DHT.
 */
export const removeMembership = async (podId, peerId) => {
  return (
    await api.delete(
      `${podMembershipBaseUrl}/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}`,
    )
  ).data;
};

/**
 * Get membership record from DHT.
 */
export const getMembership = async (podId, peerId) => {
  return (
    await api.get(
      `${podMembershipBaseUrl}/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}`,
    )
  ).data;
};

/**
 * Verify membership in a pod.
 */
export const verifyMembership = async (podId, peerId) => {
  return (
    await api.get(
      `${podMembershipBaseUrl}/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}/verify`,
    )
  ).data;
};

/**
 * Ban a member from a pod.
 */
export const banMember = async (podId, peerId, reason) => {
  return (
    await api.post(
      `${podMembershipBaseUrl}/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}/ban`,
      { reason },
    )
  ).data;
};

/**
 * Unban a member from a pod.
 */
export const unbanMember = async (podId, peerId) => {
  return (
    await api.post(
      `${podMembershipBaseUrl}/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}/unban`,
    )
  ).data;
};

/**
 * Change a member's role in a pod.
 */
export const changeMemberRole = async (podId, peerId, newRole) => {
  return (
    await api.post(
      `${podMembershipBaseUrl}/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}/role`,
      { newRole },
    )
  ).data;
};

/**
 * Get membership statistics.
 */
export const getMembershipStats = async () => {
  return (await api.get(`${podMembershipBaseUrl}/stats`)).data;
};

/**
 * Clean up expired membership records.
 */
export const cleanupExpiredMemberships = async () => {
  return (await api.post(`${podMembershipBaseUrl}/cleanup`)).data;
};

// Pod Discovery API functions

/**
 * Register a pod for discovery.
 */
export const registerPodForDiscovery = async (pod) => {
  return (await api.post(`${podDiscoveryBaseUrl}/register`, pod)).data;
};

/**
 * Unregister a pod from discovery.
 */
export const unregisterPodFromDiscovery = async (podId) => {
  return (
    await api.delete(
      `${podDiscoveryBaseUrl}/unregister/${encodeURIComponent(podId)}`,
    )
  ).data;
};

/**
 * Update pod discovery information.
 */
export const updatePodDiscovery = async (pod) => {
  return (await api.post(`${podDiscoveryBaseUrl}/update`, pod)).data;
};

/**
 * Discover pods by name.
 */
export const discoverPodsByName = async (name) => {
  return (
    await api.get(`${podDiscoveryBaseUrl}/name/${encodeURIComponent(name)}`)
  ).data;
};

/**
 * Discover pods by tag.
 */
export const discoverPodsByTag = async (tag) => {
  return (
    await api.get(`${podDiscoveryBaseUrl}/tag/${encodeURIComponent(tag)}`)
  ).data;
};

/**
 * Discover pods by multiple tags.
 */
export const discoverPodsByTags = async (tags) => {
  const tagsParameter = tags.join(',');
  return (
    await api.get(
      `${podDiscoveryBaseUrl}/tags/${encodeURIComponent(tagsParameter)}`,
    )
  ).data;
};

/**
 * Discover all pods.
 */
export const discoverAllPods = async (limit = 50) => {
  return (await api.get(`${podDiscoveryBaseUrl}/all`, { params: { limit } }))
    .data;
};

/**
 * Discover pods by content ID.
 */
export const discoverPodsByContent = async (contentId) => {
  return (
    await api.get(
      `${podDiscoveryBaseUrl}/content/${encodeURIComponent(contentId)}`,
    )
  ).data;
};

/**
 * Get pod discovery statistics.
 */
export const getPodDiscoveryStats = async () => {
  return (await api.get(`${podDiscoveryBaseUrl}/stats`)).data;
};

/**
 * Refresh pod discovery entries.
 */
export const refreshPodDiscovery = async () => {
  return (await api.post(`${podDiscoveryBaseUrl}/refresh`)).data;
};

// Pod Join/Leave API functions

/**
 * Submit a signed join request to a pod.
 */
export const requestPodJoin = async (joinRequest) => {
  return (await api.post(`${podMembershipBaseUrl}/join`, joinRequest)).data;
};

/**
 * Accept or reject a join request.
 */
export const acceptPodJoin = async (acceptance) => {
  return (await api.post(`${podMembershipBaseUrl}/join/accept`, acceptance))
    .data;
};

/**
 * Submit a signed leave request from a pod.
 */
export const requestPodLeave = async (leaveRequest) => {
  return (await api.post(`${podMembershipBaseUrl}/leave`, leaveRequest)).data;
};

/**
 * Accept a leave request.
 */
export const acceptPodLeave = async (acceptance) => {
  return (await api.post(`${podMembershipBaseUrl}/leave/accept`, acceptance))
    .data;
};

/**
 * Get pending join requests for a pod.
 */
export const getPendingJoinRequests = async (podId) => {
  return (
    await api.get(
      `${podMembershipBaseUrl}/join/pending/${encodeURIComponent(podId)}`,
    )
  ).data;
};

/**
 * Get pending leave requests for a pod.
 */
export const getPendingLeaveRequests = async (podId) => {
  return (
    await api.get(
      `${podMembershipBaseUrl}/leave/pending/${encodeURIComponent(podId)}`,
    )
  ).data;
};

/**
 * Cancel a pending join request.
 */
export const cancelJoinRequest = async (podId, peerId) => {
  return (
    await api.delete(
      `${podMembershipBaseUrl}/join/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}`,
    )
  ).data;
};

/**
 * Cancel a pending leave request.
 */
export const cancelLeaveRequest = async (podId, peerId) => {
  return (
    await api.delete(
      `${podMembershipBaseUrl}/leave/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}`,
    )
  ).data;
};

// Pod Message Routing API functions

/**
 * Manually route a pod message through the overlay network.
 */
export const routePodMessage = async (message) => {
  return (await api.post(`${podRoutingBaseUrl}/route`, message)).data;
};

/**
 * Route a pod message to specific peers.
 */
export const routePodMessageToPeers = async (message, targetPeerIds) => {
  return (
    await api.post(`${podRoutingBaseUrl}/route-to-peers`, {
      message,
      targetPeerIds,
    })
  ).data;
};

/**
 * Get pod message routing statistics.
 */
export const getPodMessageRoutingStats = async () => {
  return (await api.get(`${podRoutingBaseUrl}/stats`)).data;
};

/**
 * Check if a message has been seen for deduplication.
 */
export const checkMessageSeen = async (messageId, podId) => {
  return (
    await api.get(
      `${podRoutingBaseUrl}/seen/${encodeURIComponent(messageId)}/${encodeURIComponent(podId)}`,
    )
  ).data;
};

/**
 * Register a message as seen for deduplication.
 */
export const registerMessageSeen = async (messageId, podId) => {
  return (
    await api.post(
      `${podRoutingBaseUrl}/seen/${encodeURIComponent(messageId)}/${encodeURIComponent(podId)}`,
    )
  ).data;
};

/**
 * Clean up old seen message entries.
 */
export const cleanupSeenMessages = async () => {
  return (await api.post(`${podRoutingBaseUrl}/cleanup`)).data;
};

// Pod Message Signing API functions

/**
 * Sign a pod message.
 */
export const signPodMessage = async (message, privateKey) => {
  return (await api.post(`${podSigningBaseUrl}/sign`, { message, privateKey }))
    .data;
};

/**
 * Verify a pod message signature.
 */
export const verifyPodMessageSignature = async (message) => {
  return (await api.post(`${podSigningBaseUrl}/verify`, message)).data;
};

/**
 * Generate a new key pair for message signing.
 */
export const generateMessageKeyPair = async () => {
  return (await api.post(`${podSigningBaseUrl}/generate-keypair`)).data;
};

/**
 * Get message signing statistics.
 */
export const getMessageSigningStats = async () => {
  return (await api.get(`${podSigningBaseUrl}/stats`)).data;
};

// Pod Membership Verification API functions

/**
 * Verify membership in a pod.
 */
export const verifyPodMembership = async (podId, peerId) => {
  return (
    await api.get(
      `${podVerificationBaseUrl}/membership/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}`,
    )
  ).data;
};

/**
 * Verify a pod message authenticity.
 */
export const verifyPodMessage = async (message) => {
  return (await api.post(`${podVerificationBaseUrl}/message`, message)).data;
};

/**
 * Check if a peer has a required role in a pod.
 */
export const checkPodRole = async (podId, peerId, requiredRole) => {
  const result = (
    await api.get(
      `${podVerificationBaseUrl}/role/${encodeURIComponent(podId)}/${encodeURIComponent(peerId)}/${encodeURIComponent(requiredRole)}`,
    )
  ).data;
  return result.hasRole; // Assuming API returns { hasRole: boolean }
};

/**
 * Get membership verification statistics.
 */
export const getVerificationStats = async () => {
  return (await api.get(`${podVerificationBaseUrl}/stats`)).data;
};

/**
 * Message Storage API base URL
 */
const storageBaseUrl = `${apiBaseUrl}/pods/messages`;

/**
 * Search messages in a pod.
 */
export const searchMessages = async (
  podId,
  query,
  channelId = null,
  limit = 50,
) => {
  const parameters = { limit, query };
  if (channelId) parameters.channelId = channelId;
  return (
    await api.get(`${storageBaseUrl}/${podId}/search`, { params: parameters })
  ).data;
};

/**
 * Get message storage statistics.
 */
export const getMessageStorageStats = async () => {
  return (await api.get(`${storageBaseUrl}/stats`)).data;
};

/**
 * Clean up messages older than the specified timestamp.
 */
export const cleanupMessages = async (olderThan) => {
  return (
    await api.delete(`${storageBaseUrl}/cleanup`, { params: { olderThan } })
  ).data;
};

/**
 * Clean up messages in a specific channel older than the specified timestamp.
 */
export const cleanupChannelMessages = async (podId, channelId, olderThan) => {
  return (
    await api.delete(`${storageBaseUrl}/${podId}/${channelId}/cleanup`, {
      params: { olderThan },
    })
  ).data;
};

/**
 * Get message count for a pod and channel.
 */
export const getMessageCount = async (podId, channelId) => {
  return (await api.get(`${storageBaseUrl}/${podId}/${channelId}/count`)).data;
};

/**
 * Rebuild the full-text search index.
 */
export const rebuildSearchIndex = async () => {
  return (await api.post(`${storageBaseUrl}/rebuild-index`)).data;
};

/**
 * Vacuum the message storage database.
 */
export const vacuumDatabase = async () => {
  return (await api.post(`${storageBaseUrl}/vacuum`)).data;
};

/**
 * Message Backfill API base URL
 */
const backfillBaseUrl = `${apiBaseUrl}/pods/backfill`;

/**
 * Sync backfill for a pod on rejoin.
 */
export const syncPodBackfill = async (podId, lastSeenTimestamps) => {
  return (
    await api.post(`${backfillBaseUrl}/${podId}/sync`, lastSeenTimestamps)
  ).data;
};

/**
 * Get last seen timestamps for a pod.
 */
export const getLastSeenTimestamps = async (podId) => {
  return (await api.get(`${backfillBaseUrl}/${podId}/last-seen`)).data;
};

/**
 * Update last seen timestamp for a channel.
 */
export const updateLastSeenTimestamp = async (podId, channelId, timestamp) => {
  return (
    await api.put(
      `${backfillBaseUrl}/${podId}/${channelId}/last-seen`,
      timestamp,
    )
  ).data;
};

/**
 * Get backfill statistics.
 */
export const getBackfillStats = async () => {
  return (await api.get(`${backfillBaseUrl}/stats`)).data;
};

/**
 * Sync backfill for all pods.
 */
export const syncAllPodsBackfill = async () => {
  return (await api.post(`${backfillBaseUrl}/sync-all`)).data;
};

/**
 * Pod Opinion API base URL
 */
const opinionBaseUrl = `${apiBaseUrl}/pods`;

/**
 * Publish an opinion on a content variant.
 */
export const publishOpinion = async (podId, opinion) => {
  return (await api.post(`${opinionBaseUrl}/${podId}/opinions`, opinion)).data;
};

/**
 * Get all opinions for a content item.
 */
export const getContentOpinions = async (podId, contentId) => {
  return (
    await api.get(
      `${opinionBaseUrl}/${podId}/opinions/content/${encodeURIComponent(contentId)}`,
    )
  ).data;
};

/**
 * Get opinions for a specific variant.
 */
export const getVariantOpinions = async (podId, contentId, variantHash) => {
  return (
    await api.get(
      `${opinionBaseUrl}/${podId}/opinions/content/${encodeURIComponent(contentId)}/variant/${variantHash}`,
    )
  ).data;
};

/**
 * Get opinion statistics for a content item.
 */
export const getOpinionStatistics = async (podId, contentId) => {
  return (
    await api.get(
      `${opinionBaseUrl}/${podId}/opinions/content/${encodeURIComponent(contentId)}/stats`,
    )
  ).data;
};

/**
 * Refresh opinions for a pod from DHT.
 */
export const refreshPodOpinions = async (podId) => {
  return (await api.post(`${opinionBaseUrl}/${podId}/opinions/refresh`)).data;
};

/**
 * Get aggregated opinions with affinity weighting.
 */
export const getAggregatedOpinions = async (podId, contentId) => {
  return (
    await api.get(
      `${opinionBaseUrl}/${podId}/opinions/content/${encodeURIComponent(contentId)}/aggregated`,
    )
  ).data;
};

/**
 * Get member affinity scores.
 */
export const getMemberAffinities = async (podId) => {
  return (await api.get(`${opinionBaseUrl}/${podId}/opinions/members/affinity`))
    .data;
};

/**
 * Get consensus recommendations for content variants.
 */
export const getConsensusRecommendations = async (podId, contentId) => {
  return (
    await api.get(
      `${opinionBaseUrl}/${podId}/opinions/content/${encodeURIComponent(contentId)}/recommendations`,
    )
  ).data;
};

/**
 * Update member affinity scores.
 */
export const updateMemberAffinities = async (podId) => {
  return (
    await api.post(
      `${opinionBaseUrl}/${podId}/opinions/members/affinity/update`,
    )
  ).data;
};

/**
 * Pod Channel API base URL
 */
const channelBaseUrl = `${apiBaseUrl}/pods`;

/**
 * Create a new channel in a pod.
 */
export const createChannel = async (podId, channel) => {
  return (await api.post(`${channelBaseUrl}/${podId}/channels`, channel)).data;
};

/**
 * Get all channels in a pod.
 */
export const getChannels = async (podId) => {
  return (await api.get(`${channelBaseUrl}/${podId}/channels`)).data;
};

/**
 * Get a specific channel in a pod.
 */
export const getChannel = async (podId, channelId) => {
  return (await api.get(`${channelBaseUrl}/${podId}/channels/${channelId}`))
    .data;
};

/**
 * Update a channel in a pod.
 */
export const updateChannel = async (podId, channelId, channel) => {
  return (
    await api.put(`${channelBaseUrl}/${podId}/channels/${channelId}`, channel)
  ).data;
};

/**
 * Delete a channel from a pod.
 */
export const deleteChannel = async (podId, channelId) => {
  return (await api.delete(`${channelBaseUrl}/${podId}/channels/${channelId}`))
    .data;
};

/**
 * Content API base URL
 */
const contentBaseUrl = `${apiBaseUrl}/pods/content`;

/**
 * Validate a content ID for pod linking.
 */
export const validateContentIdForPod = async (contentId) => {
  return (await api.post(`${contentBaseUrl}/validate`, contentId)).data;
};

/**
 * Get metadata for a content ID.
 */
export const getContentMetadata = async (contentId) => {
  return (
    await api.get(`${contentBaseUrl}/metadata`, { params: { contentId } })
  ).data;
};

/**
 * Search for content that can be linked to pods.
 */
export const searchContent = async (query, domain = null, limit = 20) => {
  const parameters = { limit, query };
  if (domain) parameters.domain = domain;
  return (await api.get(`${contentBaseUrl}/search`, { params: parameters }))
    .data;
};

/**
 * Create a pod linked to specific content.
 */
export const createContentLinkedPod = async (podRequest) => {
  return (await api.post(`${contentBaseUrl}/create-pod`, podRequest)).data;
};
