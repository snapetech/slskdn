import '@testing-library/jest-dom';
import * as optionsApi from '../../../lib/options';
import AdminPolicies from './index';
import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import YAML from 'yaml';

vi.mock('../../../lib/options', () => ({
  getYaml: vi.fn(),
  updateYaml: vi.fn(),
}));

const renderPolicies = (options = {}) =>
  render(
    <AdminPolicies
      options={{
        remoteConfiguration: true,
        ...options,
      }}
    />,
  );

describe('AdminPolicies', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('surfaces the broad operator policy groups without exposing configured secrets', () => {
    renderPolicies({
      integrations: {
        webhooks: {
          notify: {
            call: {
              url: 'https://hooks.example/slskdn',
            },
            on: ['DownloadFileComplete'],
            retry: {
              attempts: 3,
            },
          },
        },
        scripts: {
          local: {
            on: ['All'],
            run: {
              command: './hook.sh',
            },
          },
        },
      },
      web: {
        authentication: {
          apiKeys: {
            automation: {
              key: 'existing-api-key-secret',
            },
          },
          jwt: {
            key: 'existing-jwt-secret',
          },
        },
        https: {
          certificate: {
            password: 'existing-pfx-secret',
          },
        },
      },
    });

    expect(screen.getByText('Actions: Webhooks and Scripts')).toBeInTheDocument();
    expect(screen.getByText('Transfer Policy')).toBeInTheDocument();
    expect(screen.getByText('Security and Access')).toBeInTheDocument();
    expect(screen.getByText('Search and Network Policy')).toBeInTheDocument();
    expect(screen.getByText('Retention and Storage')).toBeInTheDocument();
    expect(screen.getByText('JWT Key Set')).toBeInTheDocument();
    expect(screen.getByText('API Key Set')).toBeInTheDocument();
    expect(screen.getByText('Certificate Password Set')).toBeInTheDocument();
    expect(screen.queryByText('existing-api-key-secret')).not.toBeInTheDocument();
    expect(screen.queryByText('existing-jwt-secret')).not.toBeInTheDocument();
    expect(screen.queryByText('existing-pfx-secret')).not.toBeInTheDocument();
  });

  it('saves guided policy settings to YAML through the options API', async () => {
    optionsApi.getYaml.mockResolvedValue('web:\n  authentication: {}\n');
    optionsApi.updateYaml.mockResolvedValue({});
    renderPolicies();

    fireEvent.change(screen.getByLabelText('Webhook policy name'), {
      target: { value: 'ops' },
    });
    fireEvent.change(screen.getByLabelText('Webhook target URL'), {
      target: { value: 'https://hooks.example/slskdn' },
    });
    fireEvent.change(screen.getByLabelText('Webhook event names'), {
      target: { value: 'DownloadFileComplete\nPrivateMessageReceived' },
    });
    fireEvent.click(screen.getByLabelText('Ignore webhook certificate errors'));
    fireEvent.change(screen.getByLabelText('Script policy name'), {
      target: { value: 'local' },
    });
    fireEvent.change(screen.getByLabelText('Script command'), {
      target: { value: './hook.sh' },
    });
    fireEvent.change(screen.getByLabelText('API key replacement value'), {
      target: { value: 'new-api-key-secret' },
    });
    fireEvent.change(screen.getByLabelText('JWT replacement key'), {
      target: { value: 'new-jwt-secret' },
    });
    fireEvent.click(screen.getByLabelText('Enforce web security hardening'));
    fireEvent.click(screen.getByLabelText('Enable auto replace stuck downloads'));
    fireEvent.click(screen.getByLabelText('Use LAN-only DHT rendezvous'));
    fireEvent.click(screen.getByLabelText('Probe share media attributes'));

    fireEvent.click(screen.getByRole('button', { name: 'Save YAML' }));

    await waitFor(() => expect(optionsApi.updateYaml).toHaveBeenCalledTimes(1));
    const yaml = optionsApi.updateYaml.mock.calls[0][0].yaml;
    const saved = YAML.parse(yaml);

    expect(saved.integrations.webhooks.ops.on).toEqual([
      'DownloadFileComplete',
      'PrivateMessageReceived',
    ]);
    expect(saved.integrations.webhooks.ops.call.url).toBe(
      'https://hooks.example/slskdn',
    );
    expect(saved.integrations.webhooks.ops.call.ignore_certificate_errors).toBe(
      true,
    );
    expect(saved.integrations.scripts.local.run.command).toBe('./hook.sh');
    expect(saved.transfers.download.auto_replace_stuck).toBe(true);
    expect(saved.web.enforce_security).toBe(true);
    expect(saved.web.authentication.jwt.key).toBe('new-jwt-secret');
    expect(saved.web.authentication.api_keys.automation.key).toBe(
      'new-api-key-secret',
    );
    expect(saved.dht.lan_only).toBe(true);
    expect(saved.shares.probe_media_attributes).toBe(false);
    expect(saved.retention.logs).toBe(180);
  });

  it('keeps save disabled when remote configuration is off', () => {
    render(<AdminPolicies options={{ remoteConfiguration: false }} />);

    expect(
      screen.getByText(/Remote configuration is disabled/),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save YAML' })).toBeDisabled();
  });
});
