import React, { useEffect, useState } from 'react';
import { Button, Form, Message, Segment } from 'semantic-ui-react';
import api from '../../lib/api';

export default function SolidSettings() {
  const [status, setStatus] = useState(null);
  const [webId, setWebId] = useState('');
  const [resolved, setResolved] = useState(null);
  const [err, setErr] = useState('');

  const formatError = (e) => {
    const data = e?.response?.data;
    if (typeof data === 'string' && data.trim().length > 0) return data;
    if (data != null && typeof data !== 'string') {
      try {
        return JSON.stringify(data);
      } catch {
        return String(data);
      }
    }
    return e?.message ?? String(e);
  };

  useEffect(() => {
    (async () => {
      setErr('');
      try {
        const res = await api.get('/solid/status');
        setStatus(res.data);
      } catch (e) {
        const statusCode = e?.response?.status;
        if (statusCode === 404) {
          setStatus({ enabled: false });
          return;
        }

        setErr(formatError(e));
      }
    })();
  }, []);

  const resolveWebId = async () => {
    setErr('');
    setResolved(null);
    try {
      const res = await api.post('/solid/resolve-webid', { webId });
      setResolved(res.data);
    } catch (e) {
      setErr(formatError(e));
    }
  };

  return (
    <Segment data-testid="solid-root">
      <h2>Solid</h2>

      {status && !status.enabled && (
        <Message warning>
          Solid integration is disabled (Feature.Solid=false).
        </Message>
      )}

      {status && status.enabled && (
        <Message info>
          Client ID: <code>{status.clientId}</code>
          <br />
          Redirect path: <code>{status.redirectPath}</code>
        </Message>
      )}

      {err && <Message negative>{err}</Message>}

      <Form>
        <Form.Input
          label="WebID"
          placeholder="https://example.com/profile/card#me"
          value={webId}
          onChange={(e) => setWebId(e.target.value)}
          data-testid="solid-webid-input"
        />
        <Button
          primary
          type="button"
          onClick={resolveWebId}
          data-testid="solid-resolve-webid"
        >
          Resolve WebID
        </Button>
      </Form>

      {resolved && (
        <Segment>
          <pre style={{ whiteSpace: 'pre-wrap' }}>
            {JSON.stringify(resolved, null, 2)}
          </pre>
        </Segment>
      )}
    </Segment>
  );
}
