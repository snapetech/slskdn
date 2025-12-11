import './Footer.css';
import React, { Component } from 'react';
import { Icon } from 'semantic-ui-react';
import * as mesh from '../../lib/mesh';
import * as session from '../../lib/session';

const GITHUB_BASE = 'https://github.com/snapetech/slskdn';
const SLSKD_GITHUB = 'https://github.com/slskd/slskd';

class Footer extends Component {
  constructor(props) {
    super(props);
    this.state = {
      stats: null,
      interval: null,
    };
  }

  componentDidMount() {
    if (session.isLoggedIn()) {
      this.fetchStats();
      const interval = setInterval(() => this.fetchStats(), 10000); // Every 10s
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

  render() {
    const year = new Date().getFullYear();
    const { stats } = this.state;
    const isLoggedIn = session.isLoggedIn();

    // Determine if stats are connected
    const isDhtConnected = isLoggedIn && stats && stats.dht > 0;
    const isOverlayConnected = isLoggedIn && stats && stats.overlay > 0;
    const isNatResolved = isLoggedIn && stats && stats.natType && stats.natType !== 'Unknown';
    
    // Format NAT type tooltip
    const natTooltip = isLoggedIn && stats 
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
                name="sitemap" 
                className={isDhtConnected ? 'slskdn-footer-stat-icon connected' : 'slskdn-footer-stat-icon'}
                title={isLoggedIn && stats ? `DHT Nodes: ${stats.dht}` : 'DHT: Login to see stats'}
              />
              <span className="slskdn-footer-divider">|</span>
              <Icon 
                name="shield alternate" 
                className={isNatResolved ? 'slskdn-footer-stat-icon connected' : 'slskdn-footer-stat-icon'}
                title={natTooltip}
              />
              <span className="slskdn-footer-divider">|</span>
              <Icon 
                name="globe" 
                className={isOverlayConnected ? 'slskdn-footer-stat-icon connected' : 'slskdn-footer-stat-icon'}
                title={isLoggedIn && stats ? `Overlay Peers: ${stats.overlay}` : 'Overlay: Login to see stats'}
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
