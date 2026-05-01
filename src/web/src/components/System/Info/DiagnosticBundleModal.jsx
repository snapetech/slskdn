import { buildDiagnosticBundle } from '../../../lib/diagnosticBundle';
import React, { useMemo, useState } from 'react';
import { toast } from 'react-toastify';
import {
  Button,
  Header,
  Icon,
  Message,
  Modal,
  Popup,
  TextArea,
} from 'semantic-ui-react';

const copyToClipboard = async (value) => {
  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(value);
    return true;
  }

  return false;
};

const DiagnosticBundleModal = ({ options = {}, state = {} }) => {
  const [open, setOpen] = useState(false);
  const bundle = useMemo(
    () =>
      buildDiagnosticBundle({
        options,
        state,
      }),
    [options, state],
  );

  const copyBundle = async () => {
    const copied = await copyToClipboard(bundle);

    if (copied) {
      toast.success('Diagnostic bundle copied');
      return;
    }

    toast.info('Select the diagnostic bundle text to copy it manually');
  };

  return (
    <>
      <Popup
        content="Open a redacted diagnostic bundle you can copy for support. Secrets, tokens, passwords, cookies, and API keys are removed."
        position="top center"
        trigger={
          <Button
            aria-label="Open diagnostic bundle"
            disabled={!state}
            icon
            onClick={() => setOpen(true)}
          >
            <Icon name="clipboard list" />
            Diagnostic Bundle
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
          <Icon name="clipboard list" />
          Diagnostic Bundle
        </Modal.Header>
        <Modal.Content>
          <Message info>
            <Message.Header>Redacted support snapshot</Message.Header>
            <p>
              This bundle includes browser, route, setup-health summary, state,
              and option shape. It redacts secrets before display and does not
              contact the server.
            </p>
          </Message>
          <Header as="h4">Bundle</Header>
          <TextArea
            aria-label="Redacted diagnostic bundle"
            className="diagnostic-bundle-text"
            readOnly
            value={bundle}
          />
        </Modal.Content>
        <Modal.Actions>
          <Popup
            content="Copy the redacted diagnostic bundle to the clipboard."
            position="top center"
            trigger={
              <Button
                aria-label="Copy diagnostic bundle"
                onClick={copyBundle}
                primary
              >
                <Icon name="copy" />
                Copy
              </Button>
            }
          />
          <Popup
            content="Close the diagnostic bundle window without copying."
            position="top center"
            trigger={
              <Button
                aria-label="Close diagnostic bundle"
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

export default DiagnosticBundleModal;
