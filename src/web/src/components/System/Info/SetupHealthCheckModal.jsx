import {
  buildSetupHealthChecks,
  formatSetupHealthReport,
} from '../../../lib/setupHealthCheck';
import React, { useMemo, useState } from 'react';
import { toast } from 'react-toastify';
import {
  Button,
  Header,
  Icon,
  Label,
  Message,
  Modal,
  Popup,
  Statistic,
} from 'semantic-ui-react';

const statusColors = {
  fail: 'red',
  pass: 'green',
  warn: 'yellow',
};

const statusIcons = {
  fail: 'warning sign',
  pass: 'check circle',
  warn: 'exclamation triangle',
};

const copyToClipboard = async (value) => {
  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(value);
    return true;
  }

  return false;
};

const SetupHealthCheckModal = ({ options = {}, state = {} }) => {
  const [open, setOpen] = useState(false);
  const summary = useMemo(
    () =>
      buildSetupHealthChecks({
        options,
        state,
      }),
    [options, state],
  );
  const report = useMemo(() => formatSetupHealthReport(summary), [summary]);

  const copyReport = async () => {
    const copied = await copyToClipboard(report);

    if (copied) {
      toast.success('Setup health report copied');
      return;
    }

    toast.info('Select the setup health report text to copy it manually');
  };

  return (
    <>
      <Popup
        content="Open a mobile-friendly setup health check for connection, identity, shares, downloads, restart, URL base, and remote-configuration readiness."
        position="top center"
        trigger={
          <Button
            aria-label="Open setup health check"
            icon
            onClick={() => setOpen(true)}
          >
            <Icon name="heartbeat" />
            Setup Health
          </Button>
        }
      />
      <Modal
        centered={false}
        closeIcon
        onClose={() => setOpen(false)}
        open={open}
        size="large"
      >
        <Modal.Header>
          <Icon name="heartbeat" />
          Setup Health
        </Modal.Header>
        <Modal.Content>
          <Message
            className="setup-health-summary"
            color={summary.totals.fail ? 'red' : summary.totals.warn ? 'yellow' : 'green'}
          >
            <Message.Header>{summary.readiness}</Message.Header>
            <p>
              This check uses the options and state already loaded in this
              browser. It does not contact peers, validate credentials, scan
              folders, or mutate configuration.
            </p>
          </Message>
          <Statistic.Group
            className="setup-health-stats"
            size="mini"
          >
            <Statistic color="green">
              <Statistic.Value>{summary.totals.pass}</Statistic.Value>
              <Statistic.Label>Pass</Statistic.Label>
            </Statistic>
            <Statistic color="yellow">
              <Statistic.Value>{summary.totals.warn}</Statistic.Value>
              <Statistic.Label>Warn</Statistic.Label>
            </Statistic>
            <Statistic color="red">
              <Statistic.Value>{summary.totals.fail}</Statistic.Value>
              <Statistic.Label>Fail</Statistic.Label>
            </Statistic>
          </Statistic.Group>
          <div className="setup-health-grid">
            {summary.checks.map((item) => (
              <section
                className={`setup-health-card setup-health-card-${item.status}`}
                key={item.area}
              >
                <div className="setup-health-card-head">
                  <Header
                    as="h4"
                    className="setup-health-card-title"
                  >
                    <Icon name={statusIcons[item.status]} />
                    <Header.Content>{item.area}</Header.Content>
                  </Header>
                  <Label color={statusColors[item.status]}>
                    {item.status.toUpperCase()}
                  </Label>
                </div>
                <p className="setup-health-card-summary">{item.summary}</p>
                <p className="setup-health-card-evidence">{item.evidence}</p>
                <p className="setup-health-card-action">{item.action}</p>
              </section>
            ))}
          </div>
        </Modal.Content>
        <Modal.Actions>
          <Popup
            content="Copy the setup health check report for support or your own setup notes."
            position="top center"
            trigger={
              <Button
                aria-label="Copy setup health report"
                onClick={copyReport}
                primary
              >
                <Icon name="copy" />
                Copy Report
              </Button>
            }
          />
          <Popup
            content="Close the setup health check."
            position="top center"
            trigger={
              <Button
                aria-label="Close setup health check"
                onClick={() => setOpen(false)}
              >
                Close
              </Button>
            }
          />
        </Modal.Actions>
      </Modal>
    </>
  );
};

export default SetupHealthCheckModal;
