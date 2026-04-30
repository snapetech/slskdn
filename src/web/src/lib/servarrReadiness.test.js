import {
  buildServarrReadiness,
  summarizeServarrReadiness,
} from './servarrReadiness';

describe('servarrReadiness', () => {
  it('marks a fully configured integration ready', () => {
    const checks = buildServarrReadiness({
      apiKey: 'fixture-key',
      autoImportCompleted: true,
      enabled: true,
      importPathFrom: '/downloads',
      importPathTo: '/library',
      syncWantedToWishlist: true,
      url: 'http://example.invalid:8686',
    });

    expect(checks.every((check) => check.ready)).toBe(true);
    expect(summarizeServarrReadiness(checks)).toEqual({
      ready: checks.length,
      status: 'Ready',
      total: checks.length,
    });
  });

  it('flags missing setup without exposing secrets', () => {
    const checks = buildServarrReadiness({
      enabled: true,
      importPathFrom: '/downloads',
      syncWantedToWishlist: false,
    });
    const summary = summarizeServarrReadiness(checks);

    expect(summary.status).toBe('Needs Setup');
    expect(checks.find((check) => check.id === 'api-key').ready).toBe(false);
    expect(checks.find((check) => check.id === 'path-map').ready).toBe(false);
  });
});
