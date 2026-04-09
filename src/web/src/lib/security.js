import api from './api';

const baseUrl = '/security';

/**
 * Get security dashboard overview
 */
export const getDashboard = async () => {
  return (await api.get(`${baseUrl}/dashboard`)).data;
};

/**
 * Get recent security events
 * @param {number} count - Number of events to retrieve
 * @param {string} minSeverity - Minimum severity filter (Info, Low, Medium, High, Critical)
 */
export const getEvents = async (count = 100, minSeverity = null) => {
  const parameters = { count };
  if (minSeverity) parameters.minSeverity = minSeverity;
  return (await api.get(`${baseUrl}/events`, { params: parameters })).data;
};

/**
 * Get active bans
 */
export const getBans = async () => {
  return (await api.get(`${baseUrl}/bans`)).data;
};

/**
 * Ban an IP address
 */
export const banIp = async (
  ipAddress,
  reason,
  duration = null,
  permanent = false,
) => {
  return (
    await api.post(`${baseUrl}/bans/ip`, {
      duration,
      ipAddress,
      permanent,
      reason,
    })
  ).data;
};

/**
 * Unban an IP address
 */
export const unbanIp = async (ipAddress) => {
  return (
    await api.delete(`${baseUrl}/bans/ip/${encodeURIComponent(ipAddress)}`)
  ).data;
};

/**
 * Ban a username
 */
export const banUsername = async (
  username,
  reason,
  duration = null,
  permanent = false,
) => {
  return (
    await api.post(`${baseUrl}/bans/username`, {
      duration,
      permanent,
      reason,
      username,
    })
  ).data;
};

/**
 * Unban a username
 */
export const unbanUsername = async (username) => {
  return (
    await api.delete(`${baseUrl}/bans/username/${encodeURIComponent(username)}`)
  ).data;
};

/**
 * Get peer reputation
 */
export const getReputation = async (username) => {
  return (
    await api.get(`${baseUrl}/reputation/${encodeURIComponent(username)}`)
  ).data;
};

/**
 * Set peer reputation manually
 */
export const setReputation = async (username, score, reason) => {
  return (
    await api.put(`${baseUrl}/reputation/${encodeURIComponent(username)}`, {
      reason,
      score,
    })
  ).data;
};

/**
 * Get suspicious peers (low reputation)
 */
export const getSuspiciousPeers = async (limit = 50) => {
  return (
    await api.get(`${baseUrl}/reputation/suspicious`, { params: { limit } })
  ).data;
};

/**
 * Get trusted peers (high reputation)
 */
export const getTrustedPeers = async (limit = 50) => {
  return (await api.get(`${baseUrl}/reputation/trusted`, { params: { limit } }))
    .data;
};

/**
 * Get known scanners/reconnaissance
 */
export const getScanners = async () => {
  return (await api.get(`${baseUrl}/scanners`)).data;
};

/**
 * Get threat profiles from honeypots
 */
export const getThreats = async (minLevel = null) => {
  const parameters = minLevel ? { minLevel } : {};
  return (await api.get(`${baseUrl}/threats`, { params: parameters })).data;
};

/**
 * Get canary trap statistics
 */
export const getCanaryStats = async () => {
  return (await api.get(`${baseUrl}/canaries`)).data;
};

/**
 * Run entropy health check
 */
export const runEntropyCheck = async () => {
  return (await api.post(`${baseUrl}/entropy/check`)).data;
};

/**
 * Get trust disclosure permissions for a peer
 */
export const getDisclosure = async (username) => {
  return (
    await api.get(`${baseUrl}/disclosure/${encodeURIComponent(username)}`)
  ).data;
};

/**
 * Set trust tier for a peer
 */
export const setTrustTier = async (username, tier, reason) => {
  return (
    await api.put(`${baseUrl}/disclosure/${encodeURIComponent(username)}`, {
      reason,
      tier,
    })
  ).data;
};

/**
 * Get network guard statistics
 */
export const getNetworkStats = async () => {
  return (await api.get(`${baseUrl}/network`)).data;
};

/**
 * Get top connectors by IP
 */
export const getTopConnectors = async (limit = 10) => {
  return (await api.get(`${baseUrl}/network/top`, { params: { limit } })).data;
};

/**
 * Get server anomalies from paranoid mode
 */
export const getAnomalies = async (count = 100) => {
  return (await api.get(`${baseUrl}/anomalies`, { params: { count } })).data;
};

/**
 * Get adversarial settings
 */
export const getAdversarialSettings = async () => {
  return (await api.get(`${baseUrl}/adversarial`)).data;
};

/**
 * Update adversarial settings
 */
export const updateAdversarialSettings = async (settings) => {
  return (await api.put(`${baseUrl}/adversarial`, settings)).data;
};

/**
 * Get adversarial statistics
 */
export const getAdversarialStats = async () => {
  return (await api.get(`${baseUrl}/adversarial/stats`)).data;
};

/**
 * Get transport selector status
 */
export const getTransportStatus = async () => {
  return (await api.get(`${baseUrl}/transports/status`)).data;
};

/**
 * Get detailed status of all transports
 */
export const getAllTransportStatuses = async () => {
  return (await api.get(`${baseUrl}/transports`)).data;
};

/**
 * Test connectivity for all transports
 */
export const testTransportConnectivity = async () => {
  return await api.post(`${baseUrl}/transports/test`);
};

/**
 * Get Tor connectivity status
 */
export const getTorStatus = async () => {
  return (await api.get(`${baseUrl}/tor/status`)).data;
};

/**
 * Test Tor connectivity
 */
export const testTorConnectivity = async () => {
  return (await api.post(`${baseUrl}/tor/test`)).data;
};

export default {
  banIp,
  banUsername,
  getAdversarialSettings,
  getAdversarialStats,
  getAnomalies,
  getBans,
  getCanaryStats,
  getDashboard,
  getDisclosure,
  getEvents,
  getNetworkStats,
  getReputation,
  getScanners,
  getSuspiciousPeers,
  getThreats,
  getTopConnectors,
  getTorStatus,
  getTransportStatus,
  getTrustedPeers,
  runEntropyCheck,
  setReputation,
  setTrustTier,
  testTorConnectivity,
  testTransportConnectivity,
  unbanIp,
  unbanUsername,
  updateAdversarialSettings,
};
