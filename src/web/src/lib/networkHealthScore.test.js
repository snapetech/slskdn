import {
  buildNetworkHealthScore,
  formatNetworkHealthReport,
} from './networkHealthScore';

describe('networkHealthScore', () => {
  it('scores a connected mesh as healthy', () => {
    const health = buildNetworkHealthScore({
      discoveredPeers: [{ username: 'fixture-peer-b' }],
      meshPeers: [{ username: 'fixture-peer-a' }],
      stats: {
        dht: {
          dhtNodeCount: 120,
          isDhtRunning: true,
          isEnabled: true,
        },
        hashDb: {
          totalEntries: 50,
        },
        mesh: {
          connectedPeerCount: 1,
          warnings: [],
        },
        swarmJobs: [{}],
      },
    });

    expect(health.label).toBe('Healthy');
    expect(health.score).toBe(100);
    expect(health.findings).toEqual([]);
  });

  it('flags public rendezvous with no visible peers as needing attention', () => {
    const health = buildNetworkHealthScore({
      stats: {
        dht: {
          dhtNodeCount: 0,
          isDhtRunning: true,
          isEnabled: true,
        },
        hashDb: {
          totalEntries: 0,
        },
        mesh: {
          connectedPeerCount: 0,
          signatureVerificationFailures: 2,
          warnings: ['fixture warning'],
        },
      },
    });

    expect(health.label).toBe('Needs attention');
    expect(health.findings).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          area: 'Connectivity',
          severity: 'fail',
        }),
        expect.objectContaining({
          area: 'Security',
          severity: 'warn',
        }),
      ]),
    );
  });

  it('formats a copyable report', () => {
    const report = formatNetworkHealthReport(
      buildNetworkHealthScore({
        stats: {
          dht: {
            isEnabled: false,
          },
        },
      }),
    );

    expect(report).toContain('slskdN network health report');
    expect(report).toContain('Score:');
    expect(report).toContain('[INFO] DHT: DHT rendezvous disabled');
  });
});
