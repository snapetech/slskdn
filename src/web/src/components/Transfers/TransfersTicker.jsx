import './Transfers.css';
import { formatBytes } from '../../lib/util';
import React, { useMemo } from 'react';
import { Icon, Label, Popup, Segment } from 'semantic-ui-react';

const formatSpeed = (value) => {
  if (!value || value <= 0) {
    return '0 KB/s';
  }

  const [amount, unit] = formatBytes(value, 1).split(' ');
  return `${amount} ${unit}/s`;
};

const TransfersTicker = ({ direction, transfers }) => {
  const stats = useMemo(() => {
    const files =
      transfers?.flatMap((user) =>
        (user.directories || []).flatMap((directory) => directory.files || []),
      ) || [];

    const active = files.filter((file) => file.state === 'InProgress');
    const queued = files.filter((file) =>
      (file.state || '').toLowerCase().includes('queued'),
    );
    const totalSpeed = active.reduce(
      (sum, file) => sum + (file.averageSpeed || 0),
      0,
    );

    const topActive = active
      .sort((a, b) => (b.averageSpeed || 0) - (a.averageSpeed || 0))
      .slice(0, 3)
      .map((file) => ({
        filename: file.filename,
        id: file.id,
        percentComplete: file.percentComplete,
        speed: file.averageSpeed,
        username: file.username,
      }));

    return {
      activeCount: active.length,
      queuedCount: queued.length,
      totalSpeed,
      topActive,
    };
  }, [transfers]);

  return (
    <Segment
      className="transfers-ticker"
      raised
    >
      <div className="transfers-ticker-icon">
        <Icon
          name={direction}
          size="large"
        />
      </div>
      <div className="transfers-ticker-content">
        <Popup
          content={`Number of active ${direction}s and aggregate speed.`}
          position="top left"
          trigger={
            <Label
              color={stats.activeCount > 0 ? 'green' : 'grey'}
              size="large"
            >
              <Icon name="play" />
              {`${stats.activeCount} active @ ${formatSpeed(stats.totalSpeed)}`}
            </Label>
          }
        />
        <Popup
          content="Number of queued transfers waiting for slots."
          position="top left"
          trigger={
            <Label
              color={stats.queuedCount > 0 ? 'orange' : 'grey'}
              size="large"
              style={{ marginLeft: '8px' }}
            >
              <Icon name="hourglass half" />
              {`${stats.queuedCount} queued`}
            </Label>
          }
        />
        {stats.topActive.length > 0 ? (
          <div className="transfers-ticker-top">
            {stats.topActive.map((file) => (
              <Popup
                key={`${file.id || file.filename}-${file.username}`}
                content={`${file.username} · ${file.filename}`}
                position="top center"
                trigger={
                  <Label
                    basic
                    color="blue"
                    size="small"
                    style={{ marginLeft: '8px' }}
                  >
                    <Icon name="bolt" />
                    {`${file.username}: ${Math.round(file.percentComplete || 0)}% @ ${formatSpeed(file.speed)}`}
                  </Label>
                }
              />
            ))}
          </div>
        ) : (
          <Label
            basic
            color="grey"
            size="small"
            style={{ marginLeft: '8px' }}
          >
            <Icon name="info circle" />
            No active transfers right now.
          </Label>
        )}
      </div>
    </Segment>
  );
};

export default TransfersTicker;
