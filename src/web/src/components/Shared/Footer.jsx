import './Footer.css';
import React from 'react';
import { Icon } from 'semantic-ui-react';

const GITHUB_BASE = 'https://github.com/snapetech/slskdn';

const Footer = () => {
  const year = new Date().getFullYear();

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
        </span>
        <span className="slskdn-footer-divider">•</span>
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
};

export default Footer;
