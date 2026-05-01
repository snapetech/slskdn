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

const replacementIssueTypes = new Set([
  'CorruptedFile',
  'MissingTrackInRelease',
  'NonCanonicalVariant',
  'SuspectedTranscode',
  'WrongDuration',
]);

const quarantineIssueTypes = new Set([
  'CorruptedFile',
  'SuspectedTranscode',
]);

const buildReplacementQuery = (issue = {}) => {
  const parts = [
    issue.artist,
    issue.album,
    issue.title,
  ].map(normalizeText).filter(Boolean);

  if (parts.length > 0) {
    return parts.join(' ');
  }

  return normalizeText(issue.path || issue.filePath || issue.filename);
};

const getReplacementSearchIssues = (issues = []) =>
  issues.filter((issue) => replacementIssueTypes.has(issue.type));

const getQuarantineReviewIssues = (issues = []) =>
  issues.filter((issue) =>
    issue.severity === 'Critical' || quarantineIssueTypes.has(issue.type));

const getSafeFixIssues = (issues = []) =>
  issues.filter((issue) => issue.canAutoFix);

export const getLibraryHealthReplacementSearchQueries = (
  issues = [],
  { limit = 3 } = {},
) => {
  const candidates = getReplacementSearchIssues(issues)
    .map((issue) => buildReplacementQuery(issue))
    .filter(Boolean);

  return candidates
    .filter((query, index) =>
      candidates.findIndex((other) =>
        other.toLowerCase() === query.toLowerCase()) === index)
    .slice(0, limit);
};

export const getLibraryHealthSafeFixIssueIds = (
  issues = [],
  { limit = 25 } = {},
) =>
  getSafeFixIssues(issues)
    .map((issue) => issue.issueId)
    .filter(Boolean)
    .slice(0, limit);

export const getLibraryHealthQuarantineReviewItems = (
  issues = [],
  { limit = 25 } = {},
) =>
  getQuarantineReviewIssues(issues)
    .filter((issue) => issue.issueId)
    .slice(0, limit)
    .map((issue) => ({
      evidenceKey: `library-health:${issue.issueId}`,
      networkImpact:
        'Review only; approving here does not move, quarantine, search, browse, or download files.',
      reason: issue.reason || `${getIssueTypeLabel(issue.type)} requires review.`,
      searchText: buildReplacementQuery(issue),
      source: 'Library Health',
      sourceId: issue.issueId,
      title: [
        issue.artist,
        issue.album,
        issue.title,
      ].map(normalizeText).filter(Boolean).join(' - ') ||
        issue.path ||
        issue.filePath ||
        issue.filename ||
        issue.issueId,
    }));

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

export const buildLibraryHealthActionPlan = ({
  generatedAt = new Date(),
  issues = [],
  libraryPath = '',
} = {}) => {
  const safeFixes = getSafeFixIssues(issues);
  const replacementSearches = getReplacementSearchIssues(issues);
  const quarantineReviews = getQuarantineReviewIssues(issues);

  return [
    'Library Health Action Plan',
    `Generated: ${generatedAt instanceof Date ? generatedAt.toISOString() : generatedAt}`,
    `Library: ${normalizeText(libraryPath) || 'unspecified'}`,
    `Selected issues: ${issues.length}`,
    `Safe fix previews: ${safeFixes.length}`,
    `Replacement search previews: ${replacementSearches.length}`,
    `Quarantine review previews: ${quarantineReviews.length}`,
    '',
    'Selected issue plan:',
    ...(issues.length > 0
      ? issues.map((issue) => [
        `- ${issue.issueId || 'unknown'}: ${issue.severity || 'Unknown'} ${getIssueTypeLabel(issue.type)}`,
        issue.artist ? ` | ${issue.artist}` : '',
        issue.title ? ` - ${issue.title}` : '',
        issue.canAutoFix ? ' | safe fix preview' : ' | manual review',
        replacementIssueTypes.has(issue.type) ? ' | replacement search candidate' : '',
        quarantineReviews.includes(issue) ? ' | quarantine review candidate' : '',
      ].join(''))
      : ['- none']),
    '',
    'No remediation, replacement search, quarantine, or file mutation was started by this report.',
  ].join('\n');
};

export const buildLibraryHealthSafeFixManifest = ({
  generatedAt = new Date(),
  issues = [],
  libraryPath = '',
} = {}) => {
  const safeFixIssues = getSafeFixIssues(issues);

  return [
    'Library Health Safe Fix Manifest',
    `Generated: ${generatedAt instanceof Date ? generatedAt.toISOString() : generatedAt}`,
    `Library: ${normalizeText(libraryPath) || 'unspecified'}`,
    `Selected issues: ${issues.length}`,
    `Safe fix candidates: ${safeFixIssues.length}`,
    '',
    'Safe fix candidates:',
    ...(safeFixIssues.length > 0
      ? safeFixIssues.map((issue) => [
        `- ${issue.issueId || 'unknown'}: ${issue.severity || 'Unknown'} ${getIssueTypeLabel(issue.type)}`,
        issue.artist ? ` | ${issue.artist}` : '',
        issue.album ? ` | ${issue.album}` : '',
        issue.title ? ` - ${issue.title}` : '',
        issue.reason ? ` | ${issue.reason}` : '',
        issue.path || issue.filePath || issue.filename
          ? ` | target: ${normalizeText(issue.path || issue.filePath || issue.filename)}`
          : '',
      ].join(''))
      : ['- none']),
    '',
    'No remediation job, safe-fix execution, quarantine state change, search, download, or file mutation was started by this manifest.',
  ].join('\n');
};

export const buildLibraryHealthSearchSeeds = ({
  generatedAt = new Date(),
  issues = [],
  libraryPath = '',
} = {}) => {
  const queries = getLibraryHealthReplacementSearchQueries(issues, {
    limit: Number.MAX_SAFE_INTEGER,
  });
  const dedupedCandidates = queries.map((query) => ({
    issue: getReplacementSearchIssues(issues).find((issue) =>
      buildReplacementQuery(issue).toLowerCase() === query.toLowerCase()),
    query,
  }));

  return [
    'Library Health Replacement Search Seeds',
    `Generated: ${generatedAt instanceof Date ? generatedAt.toISOString() : generatedAt}`,
    `Library: ${normalizeText(libraryPath) || 'unspecified'}`,
    `Selected issues: ${issues.length}`,
    `Replacement candidates: ${dedupedCandidates.length}`,
    '',
    'Search seeds:',
    ...(dedupedCandidates.length > 0
      ? dedupedCandidates.map(({ issue, query }) => [
        `- ${query}`,
        issue.issueId ? ` | issue ${issue.issueId}` : '',
        issue.severity ? ` | ${issue.severity}` : '',
        issue.type ? ` | ${getIssueTypeLabel(issue.type)}` : '',
      ].join(''))
      : ['- none']),
    '',
    'No search, peer browse, download, quarantine, or file mutation was started by this export.',
  ].join('\n');
};

export const buildLibraryHealthQuarantinePacket = ({
  generatedAt = new Date(),
  issues = [],
  libraryPath = '',
} = {}) => {
  const reviewIssues = getQuarantineReviewIssues(issues);

  return [
    'Library Health Quarantine Review Packet',
    `Generated: ${generatedAt instanceof Date ? generatedAt.toISOString() : generatedAt}`,
    `Library: ${normalizeText(libraryPath) || 'unspecified'}`,
    `Selected issues: ${issues.length}`,
    `Quarantine review candidates: ${reviewIssues.length}`,
    '',
    'Review checklist:',
    ...(reviewIssues.length > 0
      ? reviewIssues.map((issue) => [
        `- ${issue.issueId || 'unknown'}: ${issue.severity || 'Unknown'} ${getIssueTypeLabel(issue.type)}`,
        issue.artist ? ` | ${issue.artist}` : '',
        issue.album ? ` | ${issue.album}` : '',
        issue.title ? ` - ${issue.title}` : '',
        issue.reason ? ` | ${issue.reason}` : '',
        issue.path || issue.filePath || issue.filename
          ? ` | local evidence: ${normalizeText(issue.path || issue.filePath || issue.filename)}`
          : '',
        issue.canAutoFix ? ' | safe fix preview also available' : ' | manual review required',
      ].join(''))
      : ['- none']),
    '',
    'No quarantine state, file movement, peer message, remediation job, search, download, or file mutation was started by this packet.',
  ].join('\n');
};

export default buildLibraryHealthReport;
