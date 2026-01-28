import React, { useEffect, useState } from 'react';
import { Button, Form, Message, Segment } from 'semantic-ui-react';
import { apiBaseUrl } from '../../config';

export default function SolidSettings() {
  const [status, setStatus] = useState(null);
  const [webId, setWebId] = useState('');
  const [resolved, setResolved] = useState(null);
  const [err, setErr] = useState('');

  useEffect(() => {
    (async () => {
      setErr('');
      try {
        const r = await fetch(`${apiBaseUrl}/solid/status`, {
          credentials: 'include',
        });
        if (!r.ok) {
          setStatus({ enabled: false });
          return;
        }
        setStatus(await r.json());
      } catch (e) {
        setErr(String(e));
      }
    })();
  }, []);

  const resolveWebId = async () => {
    setErr('');
    setResolved(null);
    try {
      const r = await fetch(`${apiBaseUrl}/solid/resolve-webid`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ webId }),
      });
      if (!r.ok) {
        const t = await r.text();
        throw new Error(t || `HTTP ${r.status}`);
      }
      setResolved(await r.json());
    } catch (e) {
      setErr(String(e));
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
