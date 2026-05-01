import {
  buildSetupHealthChecks,
  formatSetupHealthReport,
} from './setupHealthCheck';

describe('setupHealthCheck', () => {
  it('summarizes ready local setup state without exposing credentials', () => {
    const summary = buildSetupHealthChecks({
      options: {
        directories: {
          downloads: '/fixture/downloads',
        },
        remoteConfiguration: false,
        shares: {
          directories: ['/fixture/music'],
        },
        web: {
          urlBase: '/slskd',
        },
      },
      state: {
        connected: true,
        pendingRestart: false,
        user: {
          username: 'fixture_user',
        },
      },
    });

    expect(summary.readiness).toBe('Ready');
    expect(summary.totals).toEqual({ fail: 0, pass: 7, warn: 0 });
    expect(summary.checks.map((item) => item.area)).toContain('Web mounting');
  });

  it('flags missing transfer prerequisites with concrete actions', () => {
    const summary = buildSetupHealthChecks({
      options: {
        shares: {
          directories: [],
        },
      },
      state: {
        connected: false,
      },
    });

    expect(summary.readiness).toBe('Needs attention');
    expect(summary.totals.fail).toBe(2);
    expect(summary.totals.warn).toBeGreaterThanOrEqual(1);
    expect(summary.checks.find((item) => item.area === 'Downloads')).toMatchObject({
      status: 'fail',
      summary: 'Download path missing',
    });
  });

  it('formats a copyable report from the local checks', () => {
    const summary = buildSetupHealthChecks({
      options: {},
      state: {
        connected: false,
      },
    });
    const report = formatSetupHealthReport(summary);

    expect(report).toContain('slskdN setup health check');
    expect(report).toContain('[FAIL] Soulseek session: Not connected');
    expect(report).toContain('Action: Verify credentials');
  });
});
