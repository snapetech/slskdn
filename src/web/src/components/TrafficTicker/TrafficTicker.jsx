import './TrafficTicker.css';
import { createTransfersHubConnection } from '../../lib/hubFactory';
import React, { useEffect, useState } from 'react';
import { Icon, List, Popup } from 'semantic-ui-react';

const MAX_ACTIVITIES = 50; // Keep last 50 activities

const getActivityIcon = (direction, state) => {
  const isCompleted = state.includes('Completed');
  const isFailed = state.includes('Errored') || state.includes('Cancelled') || state.includes('TimedOut') || state.includes('Rejected');

  if (direction === 'Download') {
    if (isCompleted) {
      return isFailed ? 'x' : 'checkmark';
    }
    return 'download';
  } else {
    if (isCompleted) {
      return isFailed ? 'x' : 'checkmark';
    }
    return 'upload';
  }
};

const getActivityColor = (direction, state) => {
  const isCompleted = state.includes('Completed');
  const isFailed = state.includes('Errored') || state.includes('Cancelled') || state.includes('TimedOut') || state.includes('Rejected');

  if (isFailed) return 'red';
  if (isCompleted) return 'green';
  if (direction === 'Download') return 'blue';
  return 'teal';
};

const formatActivity = (activity) => {
  const { direction, username, filename, state, size, averageSpeed, percentComplete } = activity;
  const fileName = filename.split('\\').pop().split('/').pop();

  if (state.includes('Completed')) {
    const speedText = averageSpeed > 0 ? ` @ ${formatBytes(averageSpeed)}/s` : '';
    return `${direction === 'Download' ? '↓' : '↑'} ${username}/${fileName} ${state}${speedText}`;
  }

  if (state.includes('InProgress') && percentComplete > 0) {
    return `${direction === 'Download' ? '↓' : '↑'} ${username}/${fileName} ${percentComplete.toFixed(1)}%`;
  }

  return `${direction === 'Download' ? '↓' : '↑'} ${username}/${fileName} ${state}`;
};

const formatBytes = (bytes) => {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
};

const TrafficTicker = () => {
  const [activities, setActivities] = useState([]);
  const [connected, setConnected] = useState(false);
  const [expanded, setExpanded] = useState(false);

  useEffect(() => {
    const transfersHub = createTransfersHubConnection();

    transfersHub.on('activity', (activity) => {
      setActivities(prev => [activity, ...prev.slice(0, MAX_ACTIVITIES - 1)]);
    });

    transfersHub.onreconnecting(() => setConnected(false));
    transfersHub.onreconnected(() => setConnected(true));
    transfersHub.onclose(() => setConnected(false));

    transfersHub.start().then(() => setConnected(true));

    return () => {
      transfersHub.stop();
    };
  }, []);

  const visibleActivities = expanded ? activities : activities.slice(0, 10);
  const hasMore = activities.length > 10;

  return (
    <div className="traffic-ticker">
      <div className="traffic-ticker-header">
        <Icon name="exchange" />
        <span>Transfer Activity</span>
        <Icon
          name={connected ? 'wifi' : 'remove'}
          color={connected ? 'green' : 'red'}
          size="small"
          title={connected ? 'Connected to transfer feed' : 'Disconnected from transfer feed'}
        />
      </div>

      <div className="traffic-ticker-content">
        {activities.length === 0 ? (
          <div className="traffic-ticker-empty">
            <Icon name="clock outline" />
            <span>No recent activity</span>
          </div>
        ) : (
          <List divided relaxed>
            {visibleActivities.map((activity, index) => (
              <List.Item key={`${activity.timestamp}-${index}`}>
                <List.Content>
                  <div className="traffic-activity">
                    <Icon
                      name={getActivityIcon(activity.direction, activity.state)}
                      color={getActivityColor(activity.direction, activity.state)}
                      size="small"
                    />
                    <Popup
                      content={`${new Date(activity.timestamp).toLocaleTimeString()} - ${activity.direction} ${activity.state}`}
                      position="top left"
                      trigger={
                        <span className="traffic-activity-text">
                          {formatActivity(activity)}
                        </span>
                      }
                    />
                  </div>
                </List.Content>
              </List.Item>
            ))}
          </List>
        )}

        {hasMore && (
          <div className="traffic-ticker-toggle">
            <button
              onClick={() => setExpanded(!expanded)}
              className="traffic-ticker-toggle-button"
            >
              {expanded ? 'Show Less' : `Show ${activities.length - 10} More`}
            </button>
          </div>
        )}
      </div>
    </div>
  );
};

export default TrafficTicker;

