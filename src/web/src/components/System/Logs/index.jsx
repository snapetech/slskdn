import '../System.css';
import { createLogsHubConnection } from '../../../lib/hubFactory';
import { LoaderSegment } from '../../Shared';
import React, { Component } from 'react';
import { Button, ButtonGroup, Table } from 'semantic-ui-react';

const initialState = {
  connected: false,
  filterLevel: 'all',
  logs: [], // 'all', 'Information', 'Warning', 'Error', 'Debug'
};

const levels = {
  Debug: 'DBG',
  Error: 'ERR',
  Information: 'INF',
  Warning: 'WRN',
};

const maxLogs = 500;

class Logs extends Component {
  constructor(props) {
    super(props);

    this.state = initialState;
  }

  componentDidMount() {
    const logsHub = createLogsHubConnection();

    logsHub.on('buffer', (buffer) => {
      this.setState({
        connected: true,
        logs: buffer.reverse().slice(0, maxLogs),
      });
    });

    logsHub.on('log', (log) => {
      this.setState((previousState) => ({
        connected: true,
        logs: [log].concat(previousState.logs).slice(0, maxLogs),
      }));
    });

    logsHub.onreconnecting(() => this.setState({ connected: false }));
    logsHub.onclose((error) => {
      this.setState({ connected: false });
      if (error) {
        console.error('[Logs] Hub connection closed with error:', error);
      }
    });
    logsHub.onreconnected(() => this.setState({ connected: true }));

    logsHub.start().catch((error) => {
      console.error('[Logs] Failed to start hub connection:', error);
      this.setState({ connected: false });
    });
  }

  formatTimestamp = (timestamp) => {
    const date = new Date(timestamp);
    return `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}:${date.getSeconds().toString().padStart(2, '0')}`; // eslint-disable-line max-len
  };

  handleFilterChange = (level) => {
    this.setState({ filterLevel: level });
  };

  getFilteredLogs = () => {
    const { filterLevel, logs } = this.state;
    if (filterLevel === 'all') {
      return logs;
    }

    return logs.filter((log) => log.level === filterLevel);
  };

  render() {
    const { connected, filterLevel } = this.state;
    const filteredLogs = this.getFilteredLogs();

    return (
      <div className="logs">
        <div style={{ marginBottom: '1em' }}>
          <ButtonGroup>
            <Button
              active={filterLevel === 'all'}
              onClick={() => this.handleFilterChange('all')}
            >
              All
            </Button>
            <Button
              active={filterLevel === 'Information'}
              onClick={() => this.handleFilterChange('Information')}
            >
              Info
            </Button>
            <Button
              active={filterLevel === 'Warning'}
              onClick={() => this.handleFilterChange('Warning')}
            >
              Warn
            </Button>
            <Button
              active={filterLevel === 'Error'}
              onClick={() => this.handleFilterChange('Error')}
            >
              Error
            </Button>
            <Button
              active={filterLevel === 'Debug'}
              onClick={() => this.handleFilterChange('Debug')}
            >
              Debug
            </Button>
          </ButtonGroup>
          <span style={{ color: '#666', marginLeft: '1em' }}>
            {connected
              ? `Showing ${filteredLogs.length} of ${this.state.logs.length} logs`
              : 'Connecting to logs...'}
          </span>
        </div>
        {!connected && <LoaderSegment />}
        {connected && (
          <Table
            className="logs-table"
            compact="very"
          >
            <Table.Header>
              <Table.Row>
                <Table.HeaderCell>Timestamp</Table.HeaderCell>
                <Table.HeaderCell>Level</Table.HeaderCell>
                <Table.HeaderCell>Message</Table.HeaderCell>
              </Table.Row>
            </Table.Header>
            <Table.Body className="logs-table-body">
              {filteredLogs.length === 0 ? (
                <Table.Row>
                  <Table.Cell
                    colSpan="3"
                    textAlign="center"
                  >
                    No logs match the selected filter
                  </Table.Cell>
                </Table.Row>
              ) : (
                filteredLogs.map((log) => (
                  <Table.Row
                    disabled={log.level === 'Debug' && filterLevel !== 'Debug'}
                    key={log.timestamp}
                    negative={log.level === 'Error'}
                    warning={log.level === 'Warning'}
                  >
                    <Table.Cell>
                      {this.formatTimestamp(log.timestamp)}
                    </Table.Cell>
                    <Table.Cell>{levels[log.level] || log.level}</Table.Cell>
                    <Table.Cell className="logs-table-message">
                      {log.message}
                    </Table.Cell>
                  </Table.Row>
                ))
              )}
            </Table.Body>
          </Table>
        )}
      </div>
    );
  }
}

export default Logs;
