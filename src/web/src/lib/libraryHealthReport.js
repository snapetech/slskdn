const normalizeText = (value = '') => String(value).trim();

const getIssueTypeLabel = (type) => {
  switch (type) {
    case 'SuspectedTranscode':
      return 'Suspected Transcode';
    case 'NonCanonicalVariant':
      return 'Non-Canonical Variant';
    case 'TrackNotInTaggedRelease':
      return 'Track Not in Tagged Release';
    case 'MissingTrackInRelease':
      return 'Missing Track in Release';
    case 'CorruptedFile':
      return 'Corrupted File';
    case 'MissingMetadata':
      return 'Missing Metadata';
    case 'MultipleVariants':
      return 'Multiple Variants';
    case 'WrongDuration':
      return 'Wrong Duration';
    default:
      return normalizeText(type) || 'Unknown';
  }
};

const formatCountLine = (label, value) => `${label}: ${Number(value || 0)}`;

export const buildLibraryHealthReport = ({
  generatedAt = new Date(),
  issues = [],
  issuesByArtist = [],
  issuesByType = [],
  libraryPath = '',
  summary = {},
} = {}) => [
  'Library Health Report',
  `Generated: ${generatedAt instanceof Date ? generatedAt.toISOString() : generatedAt}`,
  `Library: ${normalizeText(libraryPath) || 'unspecified'}`,
  '',
  'Summary:',
  formatCountLine('Total issues', summary.totalIssues),
  formatCountLine('Open issues', summary.issuesOpen),
  formatCountLine('Resolved issues', summary.issuesResolved),
  '',
  'Issues by type:',
  ...(issuesByType.length > 0
    ? issuesByType.map((group) =>
      `- ${getIssueTypeLabel(group.type)}: ${Number(group.count || 0)}`)
    : ['- none']),
  '',
  'Top artists:',
  ...(issuesByArtist.length > 0
    ? issuesByArtist.map((group) =>
      `- ${normalizeText(group.artist) || 'Unknown artist'}: ${Number(group.count || 0)}`)
    : ['- none']),
  '',
  'Issue sample:',
  ...(issues.length > 0
    ? issues.slice(0, 50).map((issue) => [
      `- ${issue.severity || 'Unknown'} ${getIssueTypeLabel(issue.type)}`,
      issue.artist ? ` | ${issue.artist}` : '',
      issue.title ? ` - ${issue.title}` : '',
      issue.reason ? ` | ${issue.reason}` : '',
      issue.canAutoFix ? ' | safe fix available' : ' | review only',
    ].join(''))
    : ['- none']),
].join('\n');

export default buildLibraryHealthReport;
