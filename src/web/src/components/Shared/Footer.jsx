import './Footer.css';
import * as mesh from '../../lib/mesh';
import * as session from '../../lib/session';
import * as slskdnAPI from '../../lib/slskdn';
import { getLocalStorageItem } from '../../lib/storage';
import * as transfers from '../../lib/transfers';
import { urlBase } from '../../config';
import React, { Component } from 'react';
import { Icon } from 'semantic-ui-react';

const GITHUB_BASE = 'https://github.com/snapetech/slskdn';
const SLSKD_GITHUB = 'https://github.com/slskd/slskd';

const formatSpeed = (bytesPerSec) => {
  if (!bytesPerSec || bytesPerSec === 0) return { unit: 'B', value: '0' };

  const kb = bytesPerSec / 1_024;
  const mb = kb / 1_024;
  const gb = mb / 1_024;

  if (gb >= 1) {
    return { unit: 'G', value: gb.toFixed(gb >= 10 ? 1 : 2) };
  }

  if (mb >= 1) {
    return { unit: 'M', value: mb.toFixed(mb >= 10 ? 1 : 2) };
  }

  if (kb >= 1) {
    return { unit: 'K', value: kb.toFixed(kb >= 10 ? 1 : 2) };
  }

  return { unit: 'B', value: bytesPerSec.toFixed(0) };
};

const formatCount = (value) => {
  if (value === undefined || value === null) return '0';
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}M`;
  if (value >= 1_000) return `${(value / 1_000).toFixed(1)}K`;
  return value.toString();
};

const setFooterHeightVariable = (element) => {
  if (!element || typeof document === 'undefined') return;

  const height = Math.ceil(element.getBoundingClientRect().height);
  if (height > 0) {
    document.documentElement.style.setProperty(
      '--slskdn-footer-height',
      `${height}px`,
    );
  }
};

class Footer extends Component {
  constructor(props) {
    super(props);
    this.state = {
      interval: null,
      slskdnStats: null,
      speeds: null,
      stats: null,
    };
    this.footerRef = React.createRef();
    this.footerResizeObserver = null;
  }

  componentDidMount() {
    this.updateFooterHeight();
    if (
      typeof window !== 'undefined' &&
      typeof window.ResizeObserver === 'function' &&
      this.footerRef.current
    ) {
      this.footerResizeObserver = new window.ResizeObserver(
        this.updateFooterHeight,
      );
      this.footerResizeObserver.observe(this.footerRef.current);
    }

    if (session.isLoggedIn()) {
      this.fetchStats();
      this.fetchSpeeds();
      const interval = setInterval(() => {
        this.fetchStats();
        this.fetchSpeeds();
      }, 2_000); // Every 2s for real-time feel
      this.setState({ interval });
    }
  }

  componentWillUnmount() {
    if (this.state.interval) {
      clearInterval(this.state.interval);
    }
    if (this.footerResizeObserver) {
      this.footerResizeObserver.disconnect();
      this.footerResizeObserver = null;
    }
  }

  componentDidUpdate() {
    this.updateFooterHeight();
  }

  updateFooterHeight = () => {
    setFooterHeightVariable(this.footerRef.current);
  };

  fetchStats = async () => {
    if (!session.isLoggedIn()) {
      return;
    }

    try {
      const [transportStats, slskdnStats] = await Promise.allSettled([
        mesh.getStats(),
        slskdnAPI.getSlskdnStats(),
      ]);

      this.setState({
        slskdnStats:
          slskdnStats.status === 'fulfilled' ? slskdnStats.value : null,
        stats:
          transportStats.status === 'fulfilled' ? transportStats.value : null,
      });
    } catch (error) {
      // Silently fail - stats are non-critical
      console.debug('Failed to fetch mesh stats:', error);
    }
  };

  fetchSpeeds = async () => {
    if (!session.isLoggedIn()) {
      return;
    }

    try {
      const speeds = await transfers.getSpeeds();
      this.setState({ speeds });
    } catch (error) {
      // Silently fail - speeds are non-critical
      console.debug('Failed to fetch transfer speeds:', error);
    }
  };

  render() {
    const year = new Date().getFullYear();
    const { slskdnStats, speeds, stats } = this.state;
    const isLoggedIn = session.isLoggedIn();
    const dht = slskdnStats?.dht || {};
    const hashDb = slskdnStats?.hashDb || {};
    const meshStats = slskdnStats?.mesh || {};
    const swarmJobs = Array.isArray(slskdnStats?.swarmJobs)
      ? slskdnStats.swarmJobs
      : [];
    const dhtNodes = Number(dht.dhtNodeCount) || Number(stats?.dht) || 0;
    const discoveredPeers = Number(dht.discoveredPeerCount) || 0;
    const displayedDhtPeers = discoveredPeers || dhtNodes;
    const meshPeers = Number(meshStats.connectedPeerCount) || 0;
    const hashCount = Number(hashDb.totalEntries) || 0;
    const seqId =
      Number(hashDb.currentSeqId) || Number(meshStats.localSeqId) || 0;
    const isSyncing = Boolean(meshStats.isSyncing);
    const backfillActive = Boolean(slskdnStats?.backfill?.isActive);
    const activeSwarms = swarmJobs.length;
    const karma = Number.parseInt(getLocalStorageItem('slskdn-karma', '0'), 10);
    const totalSpeed = isLoggedIn && speeds ? formatSpeed(speeds.total) : null;
    const soulseekSpeed =
      isLoggedIn && speeds ? formatSpeed(speeds.soulseek) : null;
    const meshSpeed = isLoggedIn && speeds ? formatSpeed(speeds.mesh) : null;

    // Determine if stats are connected
    const isDhtConnected = isLoggedIn && displayedDhtPeers > 0;
    const isOverlayConnected = isLoggedIn && stats && stats.overlay > 0;
    const isNatResolved =
      isLoggedIn && stats && stats.natType && stats.natType !== 'Unknown';

    // Format NAT type tooltip
    const natTooltip =
      isLoggedIn && stats
        ? `NAT Type: ${stats.natType || 'Unknown'}`
        : 'NAT: Login to see stats';
    const networkTooltip = isLoggedIn
      ? `DHT peers: ${displayedDhtPeers}; DHT nodes: ${dhtNodes}; mesh peers: ${meshPeers}; hashes: ${hashCount}; seq: ${seqId}`
      : 'Login to see slskdN network stats';

    return (
      <footer
        className="slskdn-footer"
        ref={this.footerRef}
      >
        <div className="slskdn-footer-content">
          <div className="slskdn-footer-left">
            <div className="slskdn-footer-brand">
              <a
                className="slskdn-footer-sponsor"
                href="https://github.com/sponsors/snapetech"
                rel="noopener noreferrer"
                target="_blank"
                title="Support development - because Cursor isn't cheap!"
              >
                <Icon name="heart" /> Sponsor
              </a>

              <span className="slskdn-footer-copyright">
                © {year}{' '}
                <a
                  href={GITHUB_BASE}
                  rel="noopener noreferrer"
                  target="_blank"
                  title="slskdN project"
                >
                  slskdN
                </a>
                <span className="slskdn-footer-note">unofficial fork of</span>
                <a
                  href={SLSKD_GITHUB}
                  rel="noopener noreferrer"
                  target="_blank"
                  title="slskd upstream project"
                >
                  slskd
                </a>
              </span>
            </div>
          </div>

          <div className="slskdn-footer-center">
            <div
              className={`slskdn-footer-speeds ${isLoggedIn && speeds ? 'active' : ''}`}
              aria-label="Transfer speeds"
            >
              <span className="slskdn-footer-group-label">Speed</span>
              <span
                className="slskdn-footer-speed-item"
                title={
                  isLoggedIn
                    ? 'Total transfer speed (upload + download)'
                    : 'Login to see real-time speeds'
                }
              >
                <strong>T:</strong>{' '}
                <span className="speed-value">
                  {totalSpeed ? totalSpeed.value : '0'}
                </span>
                <span className="speed-unit">{totalSpeed ? totalSpeed.unit : 'B'}</span>
              </span>
              <span
                className="slskdn-footer-speed-item"
                title={
                  isLoggedIn
                    ? 'Soulseek network speed'
                    : 'Login to see real-time speeds'
                }
              >
                <strong>S:</strong>{' '}
                <span className="speed-value">
                  {soulseekSpeed ? soulseekSpeed.value : '0'}
                </span>
                <span className="speed-unit">
                  {soulseekSpeed ? soulseekSpeed.unit : 'B'}
                </span>
              </span>
              <span
                className="slskdn-footer-speed-item"
                title={
                  isLoggedIn
                    ? 'Mesh network speed'
                    : 'Login to see real-time speeds'
                }
              >
                <strong>M:</strong>{' '}
                <span className="speed-value">
                  {meshSpeed ? meshSpeed.value : '0'}
                </span>
                <span className="speed-unit">{meshSpeed ? meshSpeed.unit : 'B'}</span>
              </span>
            </div>
          </div>

          <div className="slskdn-footer-right">
            <a
              className={`slskdn-footer-network ${isLoggedIn && slskdnStats ? 'active' : ''}`}
              href={`${urlBase}/system/network`}
              title={networkTooltip}
            >
              <span className="slskdn-footer-group-label">Network</span>
              <span className="slskdn-footer-network-item">
                <Icon
                  color={displayedDhtPeers > 0 ? 'green' : 'grey'}
                  name="rss"
                />
                {formatCount(displayedDhtPeers)} dht
              </span>
              <span className="slskdn-footer-network-item">
                <Icon
                  color={meshPeers > 0 ? 'green' : 'grey'}
                  name="sitemap"
                />
                {formatCount(meshPeers)} mesh
              </span>
              <span className="slskdn-footer-network-item">
                <Icon
                  color={hashCount > 0 ? 'blue' : 'grey'}
                  name="database"
                />
                {formatCount(hashCount)} hashes
              </span>
              <span
                className={`slskdn-footer-network-item ${isSyncing ? 'syncing' : ''}`}
              >
                <Icon
                  color={isSyncing ? 'yellow' : 'grey'}
                  loading={isSyncing}
                  name="sync"
                />
                seq:{seqId}
              </span>
              {activeSwarms > 0 && (
                <span className="slskdn-footer-network-item active">
                  <Icon name="bolt" />
                  {activeSwarms} swarm{activeSwarms === 1 ? '' : 's'}
                </span>
              )}
              {backfillActive && (
                <span className="slskdn-footer-network-item active">
                  <Icon
                    loading
                    name="clock"
                  />
                  backfill
                </span>
              )}
              <span className="slskdn-footer-network-item">
                <Icon name="trophy" />
                {karma > 0 ? '+' : ''}
                {karma}
              </span>
            </a>

            <div
              className="slskdn-footer-stats"
              aria-label="Transport health"
            >
              <Icon
                className={
                  isDhtConnected
                    ? 'slskdn-footer-stat-icon connected'
                    : 'slskdn-footer-stat-icon'
                }
                name="sitemap"
                title={
                  isLoggedIn && stats
                    ? `DHT Nodes: ${stats.dht}`
                    : 'DHT: Login to see stats'
                }
              />
              <Icon
                className={
                  isNatResolved
                    ? 'slskdn-footer-stat-icon connected'
                    : 'slskdn-footer-stat-icon'
                }
                name="shield alternate"
                title={natTooltip}
              />
              <Icon
                className={
                  isOverlayConnected
                    ? 'slskdn-footer-stat-icon connected'
                    : 'slskdn-footer-stat-icon'
                }
                name="globe"
                title={
                  isLoggedIn && stats
                    ? `Overlay Peers: ${stats.overlay}`
                    : 'Overlay: Login to see stats'
                }
              />
            </div>
          </div>
        </div>
      </footer>
    );
  }
}

export default Footer;
