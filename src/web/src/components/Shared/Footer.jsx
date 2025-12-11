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

    return (
      <footer className="slskdn-footer">
        <div className="slskdn-footer-content">
          <span className="slskdn-footer-copyright">
            © {year}{' '}
            <a
              href={GITHUB_BASE}
              rel="noopener noreferrer"
              target="_blank"
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
          <span className="slskdn-footer-divider">•</span>
          {isLoggedIn && stats ? (
            <>
              <span className="slskdn-footer-stats" title="Mesh Transport Stats">
                <Icon name="server" /> DHT: {stats.dht} | Overlay: {stats.overlay} | NAT: {stats.natType}
              </span>
              <span className="slskdn-footer-divider">•</span>
            </>
          ) : isLoggedIn ? (
            <>
              <span className="slskdn-footer-stats" title="Loading mesh stats...">
                <Icon name="server" /> ##
              </span>
              <span className="slskdn-footer-divider">•</span>
            </>
          ) : (
            <>
              <span className="slskdn-footer-stats" title="Login to see mesh stats">
                <Icon name="server" /> ##
              </span>
              <span className="slskdn-footer-divider">•</span>
            </>
          )}
          <a
            className="slskdn-footer-link"
            href={GITHUB_BASE}
            rel="noopener noreferrer"
            target="_blank"
            title="GitHub"
          >
            <Icon name="github" /> GitHub
          </a>
          <span className="slskdn-footer-divider">•</span>
          <a
            className="slskdn-footer-link"
            href="https://discord.gg/NRzj8xycQZ"
            rel="noopener noreferrer"
            target="_blank"
            title="Discord"
          >
            <Icon name="discord" /> Discord
          </a>
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
      </footer>
    );
  }
}

export default Footer;
