import { urlBase } from '../../config';
import * as slskdnAPI from '../../lib/slskdn';
import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Icon, Label, Popup } from 'semantic-ui-react';

const STORAGE_KEY = 'slskdn-status-bar-visible';
const KARMA_STORAGE_KEY = 'slskdn-karma';

const formatNumber = (value) => {
  if (value === undefined || value === null) return '0';
  if (value >= 1_000_000) return (value / 1_000_000).toFixed(1) + 'M';
  if (value >= 1_000) return (value / 1_000).toFixed(1) + 'K';
  return value.toString();
};

// Get karma color based on value
const getKarmaColor = (karma) => {
  if (karma >= 100) return '#fbbf24'; // Gold
  if (karma >= 50) return '#22c55e'; // Green
  if (karma >= 10) return '#3b82f6'; // Blue
  return '#6b7280'; // Gray
};

// Get karma tier name
const getKarmaTier = (karma) => {
  if (karma >= 100) return 'Network Guardian';
  if (karma >= 50) return 'Trusted Relay';
  if (karma >= 10) return 'Active Helper';
  if (karma > 0) return 'Contributor';
  return 'New Member';
};

// Export for use in App.jsx
export const isStatusBarVisible = () => {
  return localStorage.getItem(STORAGE_KEY) !== 'false';
};

export const toggleStatusBarVisibility = () => {
  const current = localStorage.getItem(STORAGE_KEY) !== 'false';
  localStorage.setItem(STORAGE_KEY, !current);
  window.dispatchEvent(new Event('slskdn-status-toggle'));
  return !current;
};

// eslint-disable-next-line complexity
const SlskdnStatusBar = () => {
  const [stats, setStats] = useState({
    activeSwarms: 0,
    dhtNodes: 0,
    discoveredPeers: 0,
    hashCount: 0,
    isBeacon: null, // null = unknown, true = beacon, false = not beacon
    isSyncing: false,
    karma: 0,
    meshPeers: 0,
    seqId: 0,
    verifiedBeacons: 0,
  });
  const [visible, setVisible] = useState(() => isStatusBarVisible());

  useEffect(() => {
    const fetchStats = async () => {
      try {
        const data = await slskdnAPI.getSlskdnStats();
        // Get karma from localStorage for now (backend karma system is future work)
        const storedKarma = Number.parseInt(
          localStorage.getItem(KARMA_STORAGE_KEY) || '0',
          10,
        );

        // Safely extract values with fallbacks
        const hashDatabase = data?.hashDb || {};
        const mesh = data?.mesh || {};
        const backfill = data?.backfill || {};
        const swarmJobs = data?.swarmJobs || [];
        const dht = data?.dht || {};

        setStats((previous) => ({
          ...previous,
          activeSwarms: Array.isArray(swarmJobs) ? swarmJobs.length : 0,
          backfillActive: Boolean(backfill.isActive),
          dhtNodes: Number(dht.dhtNodeCount) || previous.dhtNodes,
          discoveredPeers:
            Number(dht.discoveredPeerCount) || previous.discoveredPeers,
          hashCount: Number(hashDatabase.totalEntries) || 0,
          isBeacon:
            dht.isBeaconCapable === undefined
              ? previous.isBeacon
              : Boolean(dht.isBeaconCapable),
          isSyncing: Boolean(mesh.isSyncing),
          karma: Number(data?.karma?.total) || storedKarma,
          meshPeers: Number(mesh.connectedPeerCount) || 0,
          seqId:
            Number(hashDatabase.currentSeqId) || Number(mesh.localSeqId) || 0,
          verifiedBeacons:
            Number(dht.verifiedBeaconCount) || previous.verifiedBeacons,
        }));
      } catch (error) {
        // Silently handle errors - status bar is non-critical
        console.debug(
          'Status bar fetch error (non-critical):',
          error?.message || error,
        );
      }
    };

    fetchStats();
    const interval = setInterval(fetchStats, 10_000);

    // Listen for toggle events from nav bar
    const handleToggle = () => setVisible(isStatusBarVisible());
    window.addEventListener('slskdn-status-toggle', handleToggle);

    return () => {
      clearInterval(interval);
      window.removeEventListener('slskdn-status-toggle', handleToggle);
    };
  }, []);

  if (!visible) return null;

  const {
    activeSwarms,
    backfillActive,
    dhtNodes,
    discoveredPeers,
    hashCount,
    isBeacon,
    isSyncing,
    karma,
    meshPeers,
    seqId,
  } = stats;

  return (
    <div className="slskdn-status-bar">
      <div className="slskdn-status-items">
        <Link
          className="slskdn-status-item"
          title={
            isBeacon === null
              ? 'Checking beacon status...'
              : isBeacon
                ? `📡 BEACON! Broadcasting to ${dhtNodes} DHT nodes. Found ${discoveredPeers} peers.`
                : `Not beacon-capable (behind NAT). Found ${discoveredPeers} peers.`
          }
          to={`${urlBase}/system/network`}
        >
          <Icon
            color={isBeacon ? 'green' : 'grey'}
            name="rss"
            size="small"
          />
          <span className={isBeacon ? 'active' : ''}>
            {discoveredPeers} dht
          </span>
        </Link>

        <span className="slskdn-status-divider">│</span>

        <Link
          className="slskdn-status-item"
          title="Connected slskdn mesh peers"
          to={`${urlBase}/system/network`}
        >
          <Icon
            color={meshPeers > 0 ? 'green' : 'grey'}
            name="sitemap"
            size="small"
          />
          <span className={meshPeers > 0 ? 'active' : ''}>
            {meshPeers} mesh
          </span>
        </Link>

        <span className="slskdn-status-divider">│</span>

        <Link
          className="slskdn-status-item"
          title="Content hashes in local database"
          to={`${urlBase}/system/network`}
        >
          <Icon
            color={hashCount > 0 ? 'blue' : 'grey'}
            name="database"
            size="small"
          />
          <span className={hashCount > 0 ? 'active' : ''}>
            {formatNumber(hashCount)} hashes
          </span>
        </Link>

        <span className="slskdn-status-divider">│</span>

        <Link
          className="slskdn-status-item"
          title="Mesh sync sequence ID"
          to={`${urlBase}/system/network`}
        >
          <Icon
            className={isSyncing ? 'loading' : ''}
            color={isSyncing ? 'yellow' : 'grey'}
            name="sync"
            size="small"
          />
          <span className={isSyncing ? 'syncing' : ''}>seq:{seqId}</span>
        </Link>

        {karma !== undefined && karma !== null && (
          <>
            <span className="slskdn-status-divider">│</span>
            <span
              className="slskdn-status-item"
              title="Your karma on the Soulseek network"
            >
              <Icon
                color={karma > 0 ? 'green' : karma < 0 ? 'red' : 'grey'}
                name="heart"
                size="small"
              />
              <span className={karma > 0 ? 'active' : ''}>
                {karma > 0 ? '+' : ''}
                {karma}
              </span>
            </span>
          </>
        )}

        {activeSwarms > 0 && (
          <>
            <span className="slskdn-status-divider">│</span>
            <Link
              className="slskdn-status-item"
              title="Active swarm downloads"
              to={`${urlBase}/downloads`}
            >
              <Label
                color="yellow"
                size="mini"
              >
                <Icon name="bolt" />
                {activeSwarms} swarm{activeSwarms === 1 ? '' : 's'}
              </Label>
            </Link>
          </>
        )}

        {backfillActive && (
          <>
            <span className="slskdn-status-divider">│</span>
            <span
              className="slskdn-status-item"
              title="Backfill active"
            >
              <Icon
                color="purple"
                loading
                name="clock"
                size="small"
              />
              <span className="backfill">backfill</span>
            </span>
          </>
        )}

        <span className="slskdn-status-divider">│</span>
        <Popup
          content={
            <div>
              <strong>{getKarmaTier(karma)}</strong>
              <p
                style={{ fontSize: '12px', margin: '4px 0 0 0', opacity: 0.8 }}
              >
                Earn karma by helping relay connections and sharing hashes
              </p>
            </div>
          }
          position="bottom center"
          size="small"
          trigger={
            <span
              className="slskdn-status-item slskdn-karma"
              title={`Karma: ${karma} - ${getKarmaTier(karma)}`}
            >
              <Icon
                name="trophy"
                size="small"
                style={{ color: getKarmaColor(karma) }}
              />
              <span style={{ color: getKarmaColor(karma) }}>{karma}</span>
            </span>
          }
        />
      </div>

      <div className="slskdn-status-right">
        <Link
          className="slskdn-status-link"
          title="View network dashboard"
          to={`${urlBase}/system/network`}
        >
          slskdn network →
        </Link>
        <button
          className="slskdn-status-close"
          onClick={() => {
            localStorage.setItem(STORAGE_KEY, 'false');
            setVisible(false);
            window.dispatchEvent(new Event('slskdn-status-toggle'));
          }}
          title="Hide status bar (click signal icon in nav to restore)"
          type="button"
        >
          ×
        </button>
      </div>
    </div>
  );
};

export default SlskdnStatusBar;
