const getPath = (value, paths) => {
  for (const path of paths) {
    const result = path.reduce((current, key) => {
      if (!current || typeof current !== 'object') return undefined;
      return current[key];
    }, value);

    if (result !== undefined && result !== null) return result;
  }

  return undefined;
};

const asArray = (value) => {
  if (Array.isArray(value)) return value;
  if (value && typeof value === 'object') return Object.values(value);
  return [];
};

const hasValue = (value) => {
  if (Array.isArray(value)) return value.length > 0;
  if (typeof value === 'string') return value.trim().length > 0;
  return value !== undefined && value !== null && value !== false;
};

const check = ({ action, area, evidence, status, summary }) => ({
  action,
  area,
  evidence,
  status,
  summary,
});

export const buildSetupHealthChecks = ({ options = {}, state = {} } = {}) => {
  const shares = asArray(
    getPath(options, [
      ['shares', 'directories'],
      ['Shares', 'Directories'],
      ['shares'],
      ['Shares'],
    ]),
  );
  const downloads = getPath(options, [
    ['directories', 'downloads'],
    ['Directories', 'Downloads'],
    ['downloads', 'directory'],
    ['Downloads', 'Directory'],
  ]);
  const username = getPath(state, [
    ['user', 'username'],
    ['User', 'Username'],
  ]);
  const urlBase = getPath(options, [
    ['web', 'urlBase'],
    ['web', 'url_base'],
    ['Web', 'UrlBase'],
    ['Web', 'Url_Base'],
  ]);
  const remoteConfiguration = getPath(options, [
    ['remoteConfiguration'],
    ['RemoteConfiguration'],
    ['web', 'remoteConfiguration'],
    ['Web', 'RemoteConfiguration'],
  ]);
  const soulseekConnected = getPath(state, [
    ['connected'],
    ['server', 'connected'],
    ['Server', 'Connected'],
  ]);
  const pendingRestart = getPath(state, [
    ['pendingRestart'],
    ['PendingRestart'],
  ]);

  const checks = [
    check({
      action: soulseekConnected
        ? 'No action needed.'
        : 'Verify credentials and network reachability before starting searches or transfers.',
      area: 'Soulseek session',
      evidence: soulseekConnected ? 'Connected state is true.' : 'Connected state is not true.',
      status: soulseekConnected ? 'pass' : 'fail',
      summary: soulseekConnected ? 'Connected' : 'Not connected',
    }),
    check({
      action: hasValue(username)
        ? 'No action needed.'
        : 'Log in or configure credentials so requests, messages, and transfer ownership have a local identity.',
      area: 'Account identity',
      evidence: hasValue(username) ? 'A local username is present.' : 'No local username is visible in state.',
      status: hasValue(username) ? 'pass' : 'warn',
      summary: hasValue(username) ? 'Identity loaded' : 'Identity missing',
    }),
    check({
      action: shares.length
        ? 'Review share paths if peers cannot browse your library.'
        : 'Add at least one shared folder before expecting uploads or public browse results.',
      area: 'Shares',
      evidence: `${shares.length} share ${shares.length === 1 ? 'entry' : 'entries'} visible in options.`,
      status: shares.length ? 'pass' : 'warn',
      summary: shares.length ? 'Shares configured' : 'No shares configured',
    }),
    check({
      action: hasValue(downloads)
        ? 'No action needed.'
        : 'Configure a download folder before starting acquisition workflows.',
      area: 'Downloads',
      evidence: hasValue(downloads) ? 'A download path is configured.' : 'No download path is visible in options.',
      status: hasValue(downloads) ? 'pass' : 'fail',
      summary: hasValue(downloads) ? 'Download path configured' : 'Download path missing',
    }),
    check({
      action: pendingRestart
        ? 'Restart after reviewing unsaved or runtime-only option changes.'
        : 'No action needed.',
      area: 'Runtime state',
      evidence: pendingRestart ? 'The daemon reports a pending restart.' : 'No pending restart is reported.',
      status: pendingRestart ? 'warn' : 'pass',
      summary: pendingRestart ? 'Restart pending' : 'No restart pending',
    }),
    check({
      action: hasValue(urlBase)
        ? 'Use the subpath smoke check before tagging release builds that change frontend tooling.'
        : 'No action needed for root-hosted Web UI deployments.',
      area: 'Web mounting',
      evidence: hasValue(urlBase) ? `Configured URL base: ${urlBase}` : 'No URL base is configured.',
      status: 'pass',
      summary: hasValue(urlBase) ? 'Subpath hosting configured' : 'Root hosting configured',
    }),
    check({
      action: remoteConfiguration
        ? 'Review admin access before exposing the Web UI beyond trusted operators.'
        : 'Use YAML save or restart flows for persistent settings changes.',
      area: 'Remote configuration',
      evidence: remoteConfiguration
        ? 'Runtime option changes are enabled.'
        : 'Runtime option changes are not enabled.',
      status: remoteConfiguration ? 'warn' : 'pass',
      summary: remoteConfiguration ? 'Runtime edits enabled' : 'Runtime edits disabled',
    }),
  ];

  const totals = checks.reduce(
    (result, item) => ({
      ...result,
      [item.status]: result[item.status] + 1,
    }),
    { fail: 0, pass: 0, warn: 0 },
  );

  return {
    checks,
    readiness:
      totals.fail > 0 ? 'Needs attention' : totals.warn > 0 ? 'Review recommended' : 'Ready',
    totals,
  };
};

export const formatSetupHealthReport = (summary) => {
  const lines = [
    'slskdN setup health check',
    `Readiness: ${summary.readiness}`,
    `Pass: ${summary.totals.pass}  Warn: ${summary.totals.warn}  Fail: ${summary.totals.fail}`,
    '',
  ];

  summary.checks.forEach((item) => {
    lines.push(`[${item.status.toUpperCase()}] ${item.area}: ${item.summary}`);
    lines.push(`Evidence: ${item.evidence}`);
    lines.push(`Action: ${item.action}`);
    lines.push('');
  });

  return lines.join('\n').trim();
};
