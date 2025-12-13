import './UserCard.css';
import * as security from '../../lib/security';
import * as users from '../../lib/users';
import React, { Component } from 'react';
import { Icon, Popup } from 'semantic-ui-react';

class UserCard extends Component {
  constructor(props) {
    super(props);
    this.state = {
      info: null,
      loading: false,
      reputation: null,
    };
  }

  componentDidMount() {
    this.fetchUserData();
  }

  componentDidUpdate(previousProps) {
    if (previousProps.username !== this.props.username) {
      this.fetchUserData();
    }
  }

  fetchUserData = async () => {
    const { username } = this.props;
    if (!username) return;

    this.setState({ loading: true });

    try {
      const [infoResponse, reputationData] = await Promise.allSettled([
        users.getInfo({ username }),
        security.getReputation(username).catch(() => null),
      ]);

      this.setState({
        info:
          infoResponse.status === 'fulfilled' && infoResponse.value?.data
            ? infoResponse.value.data
            : null,
        loading: false,
        reputation:
          reputationData.status === 'fulfilled' && reputationData.value
            ? reputationData.value
            : null,
      });
    } catch {
      this.setState({ loading: false });
    }
  };

  getReputationColor = (score) => {
    if (score === null || score === undefined) return 'grey';
    if (score >= 80) return 'purple'; // Amazing (80-100)
    if (score >= 60) return 'green'; // Good (60-79)
    if (score >= 40) return 'olive'; // OK (40-59)
    if (score >= 20) return 'orange'; // Poor (20-39)
    return 'red'; // Very poor (0-19)
  };

  render() {
    const { children, inline = true, username } = this.props;
    const { info, loading, reputation } = this.state;

    const reputationScore = reputation?.score ?? null;
    const reputationColor = this.getReputationColor(reputationScore);

    // Build compact stats display - always show placeholders if no data
    const stats = [];

    // Reputation shield badge (always show, grayed if no data)
    stats.push({
      color: reputationScore === null ? 'grey' : reputationColor,
      glow: reputationScore !== null && reputationScore >= 70, // Glow effect for high reputation
      icon: 'shield alternate',
      key: 'rep',
      tooltip:
        reputationScore === null
          ? 'Reputation unavailable'
          : `Reputation Score: ${reputationScore}/100`,
      value: reputationScore === null ? '?' : reputationScore,
    });

    // Upload speed (always show, grayed if no data)
    if (info?.uploadSpeed === undefined) {
      stats.push({
        color: 'grey',
        icon: 'arrow up',
        key: 'speed',
        tooltip: 'Speed unavailable',
        value: '?',
      });
    } else {
      const speedKbps = Math.round(info.uploadSpeed / 1_024);
      stats.push({
        color: speedKbps > 1_000 ? 'green' : speedKbps > 500 ? 'blue' : 'grey',
        icon: 'arrow up',
        key: 'speed',
        tooltip: `Upload Speed: ${speedKbps} KB/s`,
        value: `${speedKbps}`,
      });
    }

    // Queue length (always show, grayed if no data)
    if (info?.queueLength === undefined) {
      stats.push({
        color: 'grey',
        icon: 'list',
        key: 'queue',
        tooltip: 'Queue unavailable',
        value: '?',
      });
    } else {
      stats.push({
        color:
          info.queueLength === 0
            ? 'green'
            : info.queueLength < 5
              ? 'blue'
              : 'orange',
        icon: 'list',
        key: 'queue',
        tooltip: `Queue Length: ${info.queueLength}`,
        value: info.queueLength,
      });
    }

    // Free slot (always show, grayed if no data)
    if (info?.hasFreeUploadSlot === undefined) {
      stats.push({
        color: 'grey',
        icon: 'unlock',
        key: 'slot',
        tooltip: 'Slot status unavailable',
        value: '?',
      });
    } else {
      stats.push({
        color: info.hasFreeUploadSlot ? 'green' : 'red',
        icon: 'unlock',
        key: 'slot',
        tooltip: info.hasFreeUploadSlot
          ? 'Free upload slot available'
          : 'No free slots',
        value: info.hasFreeUploadSlot ? '✓' : '✗',
      });
    }

    // Render the card
    const cardContent = (
      <span className={`user-card ${inline ? 'user-card-inline' : ''}`}>
        <span className="user-card-username">{children || username}</span>
        {loading ? (
          <span className="user-card-loading">
            <Icon
              loading
              name="spinner"
              size="small"
            />
          </span>
        ) : (
          <span className="user-card-stats">
            {stats.map((stat) => (
              <Popup
                content={stat.tooltip}
                key={stat.key}
                position="top center"
                size="tiny"
                trigger={
                  <span
                    className={`user-card-stat user-card-stat-${stat.color}${stat.glow ? ' user-card-stat-glow' : ''}`}
                  >
                    <Icon
                      fitted
                      name={stat.icon}
                      size="small"
                    />
                    <span className="user-card-stat-value">{stat.value}</span>
                  </span>
                }
              />
            ))}
          </span>
        )}
      </span>
    );

    return cardContent;
  }
}

export default UserCard;














