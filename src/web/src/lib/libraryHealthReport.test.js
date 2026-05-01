import {
  buildLibraryHealthActionPlan,
  buildLibraryHealthQuarantinePacket,
  buildLibraryHealthReport,
  buildLibraryHealthSafeFixManifest,
  buildLibraryHealthSearchSeeds,
} from './libraryHealthReport';

describe('libraryHealthReport', () => {
  it('builds a review-only text report from loaded health data', () => {
    const report = buildLibraryHealthReport({
      generatedAt: '2026-04-30T21:25:00.000Z',
      issues: [
        {
          artist: 'Fixture Artist',
          canAutoFix: true,
          reason: 'Fixture reason',
          severity: 'High',
          title: 'Fixture Track',
          type: 'SuspectedTranscode',
        },
      ],
      issuesByArtist: [{ artist: 'Fixture Artist', count: 1 }],
      issuesByType: [{ type: 'SuspectedTranscode', count: 1 }],
      libraryPath: '/fixture/music',
      summary: {
        issuesOpen: 1,
        issuesResolved: 2,
        totalIssues: 3,
      },
    });

    expect(report).toContain('Library Health Report');
    expect(report).toContain('Library: /fixture/music');
    expect(report).toContain('Total issues: 3');
    expect(report).toContain('- Suspected Transcode: 1');
    expect(report).toContain('- Fixture Artist: 1');
    expect(report).toContain('High Suspected Transcode | Fixture Artist - Fixture Track | Fixture reason | safe fix available');
  });

  it('builds a selected issue action plan without applying fixes', () => {
    const plan = buildLibraryHealthActionPlan({
      generatedAt: '2026-04-30T21:35:00.000Z',
      issues: [
        {
          artist: 'Fixture Artist',
          canAutoFix: true,
          issueId: 'issue-1',
          severity: 'Critical',
          title: 'Fixture Track',
          type: 'SuspectedTranscode',
        },
      ],
      libraryPath: '/fixture/music',
    });

    expect(plan).toContain('Library Health Action Plan');
    expect(plan).toContain('Selected issues: 1');
    expect(plan).toContain('Safe fix previews: 1');
    expect(plan).toContain('Replacement search previews: 1');
    expect(plan).toContain('Quarantine review previews: 1');
    expect(plan).toContain('replacement search candidate');
    expect(plan).toContain('No remediation, replacement search, quarantine, or file mutation was started');
  });

  it('builds deduped replacement search seeds from selected health issues', () => {
    const seeds = buildLibraryHealthSearchSeeds({
      generatedAt: '2026-04-30T21:45:00.000Z',
      issues: [
        {
          album: 'Fixture Album',
          artist: 'Fixture Artist',
          issueId: 'issue-1',
          severity: 'High',
          title: 'Fixture Track',
          type: 'WrongDuration',
        },
        {
          album: 'Fixture Album',
          artist: 'Fixture Artist',
          issueId: 'issue-2',
          severity: 'Medium',
          title: 'Fixture Track',
          type: 'WrongDuration',
        },
        {
          artist: 'Metadata Artist',
          issueId: 'issue-3',
          severity: 'Low',
          title: 'Needs Tags',
          type: 'MissingMetadata',
        },
      ],
      libraryPath: '/fixture/music',
    });

    expect(seeds).toContain('Library Health Replacement Search Seeds');
    expect(seeds).toContain('Selected issues: 3');
    expect(seeds).toContain('Replacement candidates: 1');
    expect(seeds).toContain('- Fixture Artist Fixture Album Fixture Track | issue issue-1 | High | Wrong Duration');
    expect(seeds).not.toContain('Metadata Artist Needs Tags');
    expect(seeds).toContain('No search, peer browse, download, quarantine, or file mutation was started');
  });

  it('builds a quarantine review packet only for risky selected issues', () => {
    const packet = buildLibraryHealthQuarantinePacket({
      generatedAt: '2026-04-30T21:50:00.000Z',
      issues: [
        {
          album: 'Fixture Album',
          artist: 'Fixture Artist',
          canAutoFix: false,
          issueId: 'issue-1',
          path: '/fixture/music/fixture.flac',
          reason: 'Fixture risk evidence',
          severity: 'Critical',
          title: 'Fixture Track',
          type: 'CorruptedFile',
        },
        {
          artist: 'Low Artist',
          issueId: 'issue-2',
          severity: 'Low',
          title: 'Needs Tags',
          type: 'MissingMetadata',
        },
      ],
      libraryPath: '/fixture/music',
    });

    expect(packet).toContain('Library Health Quarantine Review Packet');
    expect(packet).toContain('Selected issues: 2');
    expect(packet).toContain('Quarantine review candidates: 1');
    expect(packet).toContain('issue-1: Critical Corrupted File');
    expect(packet).toContain('Fixture risk evidence');
    expect(packet).toContain('local evidence: /fixture/music/fixture.flac');
    expect(packet).not.toContain('Low Artist');
    expect(packet).toContain('No quarantine state, file movement, peer message, remediation job, search, download, or file mutation was started');
  });

  it('builds a safe-fix manifest only for auto-fixable selected issues', () => {
    const manifest = buildLibraryHealthSafeFixManifest({
      generatedAt: '2026-04-30T21:55:00.000Z',
      issues: [
        {
          album: 'Fixture Album',
          artist: 'Fixture Artist',
          canAutoFix: true,
          issueId: 'issue-1',
          path: '/fixture/music/fixable.flac',
          reason: 'Fixture safe fix evidence',
          severity: 'Medium',
          title: 'Fixture Track',
          type: 'MissingMetadata',
        },
        {
          artist: 'Manual Artist',
          canAutoFix: false,
          issueId: 'issue-2',
          severity: 'High',
          title: 'Manual Track',
          type: 'WrongDuration',
        },
      ],
      libraryPath: '/fixture/music',
    });

    expect(manifest).toContain('Library Health Safe Fix Manifest');
    expect(manifest).toContain('Selected issues: 2');
    expect(manifest).toContain('Safe fix candidates: 1');
    expect(manifest).toContain('issue-1: Medium Missing Metadata');
    expect(manifest).toContain('Fixture safe fix evidence');
    expect(manifest).toContain('target: /fixture/music/fixable.flac');
    expect(manifest).not.toContain('Manual Artist');
    expect(manifest).toContain('No remediation job, safe-fix execution, quarantine state change, search, download, or file mutation was started');
  });
});
