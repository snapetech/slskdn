const getTotalSize = (files) =>
  files.reduce((total, file) => total + (file.size || 0), 0);

const unique = (values) => [...new Set(values.filter(Boolean))];

export const buildSearchActionPreview = ({
  candidateRank,
  communityQualitySummary,
  files = [],
  response = {},
  route = 'download',
} = {}) => {
  const selectedFiles = Array.isArray(files) ? files : [];
  const providerLabels = response.sourceProviders?.length
    ? response.sourceProviders
    : ['soulseek'];
  const lockedCount = selectedFiles.filter((file) => file.locked).length;
  const warnings = [];

  if (lockedCount > 0) {
    warnings.push(`${lockedCount} selected file${lockedCount === 1 ? '' : 's'} may be locked`);
  }

  if (!response.hasFreeUploadSlot) {
    warnings.push('No free upload slot is currently advertised');
  }

  if ((response.queueLength || 0) >= 5) {
    warnings.push(`Queue depth is ${response.queueLength}`);
  }

  if ((communityQualitySummary?.score || 0) <= -6) {
    warnings.push('Local caution signals exist for this peer');
  }

  if ((candidateRank?.score || 0) > 0 && candidateRank.score < 45) {
    warnings.push(`Candidate score is ${candidateRank.score}/100`);
  }

  return {
    candidateScore: candidateRank?.score ?? null,
    fileCount: selectedFiles.length,
    filenames: selectedFiles.map((file) => file.filename),
    lockedCount,
    providerLabels,
    route,
    totalSizeBytes: getTotalSize(selectedFiles),
    username: response.username || '',
    warnings: unique(warnings),
  };
};

export const formatSearchActionPreview = (preview) => {
  const lines = [
    `Action: ${preview.route}`,
    `Source: ${preview.username || 'unknown'}`,
    `Providers: ${preview.providerLabels.join(', ')}`,
    `Files: ${preview.fileCount}`,
    `Total bytes: ${preview.totalSizeBytes}`,
  ];

  if (preview.candidateScore !== null) {
    lines.push(`Candidate score: ${preview.candidateScore}/100`);
  }

  if (preview.warnings.length > 0) {
    lines.push('Warnings:');
    preview.warnings.forEach((warning) => lines.push(`- ${warning}`));
  }

  lines.push('Selected files:');
  preview.filenames.forEach((filename) => lines.push(`- ${filename}`));

  return lines.join('\n');
};
