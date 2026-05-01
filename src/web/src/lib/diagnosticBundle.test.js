import {
  buildDiagnosticBundle,
  redactDiagnosticValue,
} from './diagnosticBundle';

describe('diagnosticBundle', () => {
  it('redacts sensitive nested keys', () => {
    expect(
      redactDiagnosticValue({
        apiKey: 'abc',
        nested: {
          password: 'secret',
          username: 'fixture_user',
        },
        tokenValue: 'tok',
      }),
    ).toEqual({
      apiKey: '[redacted]',
      nested: {
        password: '[redacted]',
        username: 'fixture_user',
      },
      tokenValue: '[redacted]',
    });
  });

  it('redacts sensitive query-like string values', () => {
    expect(
      redactDiagnosticValue(
        'https://example.test/callback?token=abc123&safe=yes secret=bad',
      ),
    ).toBe('https://example.test/callback?token=[redacted]&safe=yes secret=[redacted]');
  });

  it('builds a copyable diagnostic bundle without secrets', () => {
    const bundle = buildDiagnosticBundle({
      browser: {
        language: 'en-US',
        platform: 'Linux',
        userAgent: 'Vitest',
      },
      generatedAt: '2026-04-30T20:10:00.000Z',
      location: {
        hash: '#mesh',
        host: 'example.invalid:5030',
        pathname: '/system/info',
        protocol: 'http:',
      },
      options: {
        directories: {
          downloads: '/fixture/downloads',
        },
        shares: {
          directories: ['/fixture/music'],
        },
        soulseek: {
          username: 'fixture_user',
          password: 'plaintext',
        },
        web: {
          authentication: {
            apiKey: 'secret-api-key',
          },
        },
      },
      state: {
        apiToken: 'abc',
        connected: true,
        user: {
          username: 'fixture_user',
        },
      },
    });

    expect(bundle).toContain('generatedAt: 2026-04-30T20:10:00.000Z');
    expect(bundle).toContain('username: fixture_user');
    expect(bundle).toContain('setupHealth:');
    expect(bundle).toContain('readiness: Ready');
    expect(bundle).toContain('score: 100');
    expect(bundle).toContain('password: "[redacted]"');
    expect(bundle).toContain('apiToken: "[redacted]"');
    expect(bundle).not.toContain('plaintext');
    expect(bundle).not.toContain('secret-api-key');
  });
});
