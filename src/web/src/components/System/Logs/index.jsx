import '../System.css';
import { createLogsHubConnection } from '../../../lib/hubFactory';
import { LoaderSegment } from '../../Shared';
import React, { Component } from 'react';
import { Button, ButtonGroup, Table } from 'semantic-ui-react';

const initialState = {
  connected: false,
  logs: [],
  filterLevel: 'all', // 'all', 'Information', 'Warning', 'Error', 'Debug'
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
    logsHub.onclose(() => this.setState({ connected: false }));
    logsHub.onreconnected(() => this.setState({ connected: true }));

    logsHub.start();
  }

  formatTimestamp = (timestamp) => {
    const date = new Date(timestamp);
    return `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}:${date.getSeconds().toString().padStart(2, '0')}`; // eslint-disable-line max-len
  };

  handleFilterChange = (level) => {
    this.setState({ filterLevel: level });
  };

  getFilteredLogs = () => {
    const { logs, filterLevel } = this.state;
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
        {!connected && <LoaderSegment />}
        {connected && (
          <>
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
              <span style={{ marginLeft: '1em', color: '#666' }}>
                Showing {filteredLogs.length} of {this.state.logs.length} logs
              </span>
            </div>
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
                {filteredLogs.map((log) => (
                  <Table.Row
                    disabled={log.level === 'Debug' && filterLevel !== 'Debug'}
                    key={log.timestamp}
                    negative={log.level === 'Error'}
                    warning={log.level === 'Warning'}
                  >
                    <Table.Cell>{this.formatTimestamp(log.timestamp)}</Table.Cell>
                    <Table.Cell>{levels[log.level] || log.level}</Table.Cell>
                    <Table.Cell className="logs-table-message">
                      {log.message}
                    </Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          </>
        )}
      </div>
    );
  }
}

export default Logs;
