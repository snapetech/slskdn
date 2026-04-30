import { buildLibraryHealthReport } from './libraryHealthReport';

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
});
