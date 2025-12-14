import './Footer.css';
import * as mesh from '../../lib/mesh';
import * as session from '../../lib/session';
import * as transfers from '../../lib/transfers';
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

class Footer extends Component {
  constructor(props) {
    super(props);
    this.state = {
      interval: null,
      speeds: null,
      stats: null,
    };
  }

  componentDidMount() {
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
  }

  fetchStats = async () => {
    if (!session.isLoggedIn()) {
      return;
    }

    try {
      const stats = await mesh.getStats();
      this.setState({ stats });
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
    const { speeds, stats } = this.state;
    const isLoggedIn = session.isLoggedIn();

    // Determine if stats are connected
    const isDhtConnected = isLoggedIn && stats && stats.dht > 0;
    const isOverlayConnected = isLoggedIn && stats && stats.overlay > 0;
    const isNatResolved =
      isLoggedIn && stats && stats.natType && stats.natType !== 'Unknown';

    // Format NAT type tooltip
    const natTooltip =
      isLoggedIn && stats
        ? `NAT Type: ${stats.natType || 'Unknown'}`
        : 'NAT: Login to see stats';

    return (
      <footer className="slskdn-footer">
        <div className="slskdn-footer-content">
          {/* Left: Sponsor link */}
          <div className="slskdn-footer-left">
            <a
              className="slskdn-footer-sponsor"
              href="https://github.com/sponsors/snapetech"
              rel="noopener noreferrer"
              target="_blank"
              title="Support development - because Cursor isn't cheap!"
            >
              <Icon name="heart" /> Sponsor
            </a>
          </div>

          {/* Center-Left: Transfer Speeds */}
          <div
            className={`slskdn-footer-speeds ${isLoggedIn && speeds ? 'active' : ''}`}
          >
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
                {isLoggedIn && speeds ? formatSpeed(speeds.total).value : '0'}
              </span>
              <span className="speed-unit">
                {isLoggedIn && speeds ? formatSpeed(speeds.total).unit : 'B'}
              </span>
            </span>
            <span className="slskdn-footer-divider">•</span>
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
                {isLoggedIn && speeds
                  ? formatSpeed(speeds.soulseek).value
                  : '0'}
              </span>
              <span className="speed-unit">
                {isLoggedIn && speeds ? formatSpeed(speeds.soulseek).unit : 'B'}
              </span>
            </span>
            <span className="slskdn-footer-divider">•</span>
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
                {isLoggedIn && speeds ? formatSpeed(speeds.mesh).value : '0'}
              </span>
              <span className="speed-unit">
                {isLoggedIn && speeds ? formatSpeed(speeds.mesh).unit : 'B'}
              </span>
            </span>
          </div>

          {/* Center: Copyright */}
          <div className="slskdn-footer-center">
            <span className="slskdn-footer-copyright">
              © {year}{' '}
              <a
                href={GITHUB_BASE}
                rel="noopener noreferrer"
                target="_blank"
                title="slskdN - the fork"
              >
                slskdN
              </a>
              {' · built on the most excellent '}
              <a
                href={SLSKD_GITHUB}
                rel="noopener noreferrer"
                target="_blank"
                title="slskd - the original project"
              >
                slskd
              </a>
            </span>
          </div>

          {/* Right: Stats icons and quote */}
          <div className="slskdn-footer-right">
            <div className="slskdn-footer-stats">
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
              <span className="slskdn-footer-divider">|</span>
              <Icon
                className={
                  isNatResolved
                    ? 'slskdn-footer-stat-icon connected'
                    : 'slskdn-footer-stat-icon'
                }
                name="shield alternate"
                title={natTooltip}
              />
              <span className="slskdn-footer-divider">|</span>
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
            <span className="slskdn-footer-divider">•</span>
            <span className="slskdn-footer-quote">
              <a
                className="slskdn-footer-emoji-link"
                href={`${GITHUB_BASE}#what-is-slskdn`}
                rel="noopener noreferrer"
                target="_blank"
                title="What is slskdN?"
              >
                <em>"slop on top"</em>
              </a>{' '}
              <a
                className="slskdn-footer-emoji-link"
                href={`${GITHUB_BASE}#what-is-slskdn`}
                rel="noopener noreferrer"
                target="_blank"
                title="Our Philosophy"
              >
                🍦
              </a>
              <a
                className="slskdn-footer-emoji-link"
                href={`${GITHUB_BASE}#use-of-ai-in-this-project`}
                rel="noopener noreferrer"
                target="_blank"
                title="AI-Assisted Development"
              >
                🤖
              </a>
              <a
                className="slskdn-footer-emoji-link"
                href={`${GITHUB_BASE}#features`}
                rel="noopener noreferrer"
                target="_blank"
                title="Features"
              >
                ✨
              </a>
            </span>
          </div>
        </div>
      </footer>
    );
  }
}

export default Footer;
