import api from './api';

// Helper to safely call API endpoints that may not exist yet
const safeGet = async (endpoint, fallback = null) => {
  try {
    const response = await api.get(endpoint);
    return response.data;
  } catch (error) {
    // Return fallback for 404s or other errors - endpoint may not be implemented
    if (error?.response?.status === 404) {
      console.debug(`Endpoint ${endpoint} not found (expected during development)`);
    }
    return fallback;
  }
};

// Capabilities API
export const getCapabilities = async () => {
  return safeGet('/capabilities', { features: [] });
};

export const getDiscoveredPeers = async () => {
  return safeGet('/capabilities/peers', { peers: [] });
};

// HashDatabase API
export const getHashDatabaseStats = async () => {
  return safeGet('/hashdb/stats', { totalHashEntries: 0, currentSeqId: 0 });
};

export const getHashDatabaseEntries = async (limit = 100, offset = 0) => {
  return safeGet(`/hashdb/entries?limit=${limit}&offset=${offset}`, { entries: [] });
};

// Mesh API
export const getMeshStats = async () => {
  return safeGet('/mesh/stats', { knownMeshPeers: 0, currentSeqId: 0, isSyncing: false });
};

export const getMeshPeers = async () => {
  return safeGet('/mesh/peers', { count: 0, peers: [] });
};

export const triggerMeshSync = async (username) => {
  try {
    return (await api.post(`/mesh/sync/${username}`)).data;
  } catch (error) {
    return { success: false, error: error?.message || 'Sync failed' };
  }
};

// Backfill API
export const getBackfillStats = async () => {
  return safeGet('/backfill/stats', { isActive: false, isRunning: false });
};

export const getBackfillCandidates = async (limit = 50) => {
  return safeGet(`/backfill/candidates?limit=${limit}`, { candidates: [] });
};

// MultiSource API
export const getActiveSwarmJobs = async () => {
  return safeGet('/multisource/jobs', []);
};

export const getSwarmJob = async (jobId) => {
  return safeGet(`/multisource/jobs/${jobId}`, null);
};

// Combined stats fetch for dashboard
export const getSlskdnStats = async () => {
  try {
    const [capabilities, hashDatabase, mesh, backfill, swarmJobs] =
      await Promise.allSettled([
        getCapabilities(),
        getHashDatabaseStats(),
        getMeshStats(),
        getBackfillStats(),
        getActiveSwarmJobs(),
      ]);

    // Normalize hashDb response to match frontend expectations
    const rawHashDb = hashDatabase.status === 'fulfilled' ? hashDatabase.value : null;
    const normalizedHashDb = rawHashDb ? {
      ...rawHashDb,
      // Map backend field names to frontend expectations
      totalEntries: rawHashDb.totalHashEntries ?? rawHashDb.totalEntries ?? 0,
      currentSeqId: rawHashDb.currentSeqId ?? 0,
    } : { totalEntries: 0, currentSeqId: 0 };

    // Normalize mesh response to match frontend expectations
    const rawMesh = mesh.status === 'fulfilled' ? mesh.value : null;
    const normalizedMesh = rawMesh ? {
      ...rawMesh,
      // Map backend field names to frontend expectations
      connectedPeerCount: rawMesh.knownMeshPeers ?? rawMesh.connectedPeerCount ?? 0,
      localSeqId: rawMesh.currentSeqId ?? rawMesh.localSeqId ?? 0,
      isSyncing: rawMesh.isSyncing ?? false,
    } : { connectedPeerCount: 0, localSeqId: 0, isSyncing: false };

    // Normalize backfill response
    const rawBackfill = backfill.status === 'fulfilled' ? backfill.value : null;
    const normalizedBackfill = rawBackfill ? {
      ...rawBackfill,
      isActive: rawBackfill.isActive ?? rawBackfill.isRunning ?? false,
    } : { isActive: false };

    return {
      backfill: normalizedBackfill,
      capabilities:
        capabilities.status === 'fulfilled' ? capabilities.value : null,
      hashDb: normalizedHashDb,
      mesh: normalizedMesh,
      swarmJobs: swarmJobs.status === 'fulfilled' ? swarmJobs.value : [],
    };
  } catch (error) {
    console.error('Failed to fetch slskdn stats:', error);
    return {
      backfill: { isActive: false },
      capabilities: null,
      hashDb: { totalEntries: 0, currentSeqId: 0 },
      mesh: { connectedPeerCount: 0, localSeqId: 0, isSyncing: false },
      swarmJobs: [],
    };
  }
};
