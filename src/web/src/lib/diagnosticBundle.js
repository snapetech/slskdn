import { buildSetupHealthChecks } from './setupHealthCheck';
import YAML from 'yaml';

const sensitiveKeyPattern =
  /(api[-_]?key|authorization|cookie|credential|jwt|pass(word)?|secret|session|token)/iu;

const redactString = (value) =>
  value.replace(
    /(api[-_]?key|authorization|password|secret|token)=([^&\s]+)/giu,
    '$1=[redacted]',
  );

export const redactDiagnosticValue = (value, key = '') => {
  if (sensitiveKeyPattern.test(key)) {
    return '[redacted]';
  }

  if (Array.isArray(value)) {
    return value.map((item) => redactDiagnosticValue(item));
  }

  if (value && typeof value === 'object') {
    return Object.entries(value).reduce((result, [entryKey, entryValue]) => {
      result[entryKey] = redactDiagnosticValue(entryValue, entryKey);
      return result;
    }, {});
  }

  if (typeof value === 'string') {
    return redactString(value);
  }

  return value;
};

export const buildDiagnosticBundle = ({
  browser = typeof window === 'undefined' ? undefined : window.navigator,
  generatedAt = new Date().toISOString(),
  location = typeof window === 'undefined' ? undefined : window.location,
  options = {},
  state = {},
} = {}) => {
  const setupHealth = buildSetupHealthChecks({
    options,
    state,
  });
  const bundle = {
    browser: {
      language: browser?.language,
      platform: browser?.platform,
      userAgent: browser?.userAgent,
    },
    generatedAt,
    location: {
      hash: location?.hash,
      host: location?.host,
      pathname: location?.pathname,
      protocol: location?.protocol,
    },
    options: redactDiagnosticValue(options),
    setupHealth: {
      checks: setupHealth.checks.map((item) => ({
        action: item.action,
        area: item.area,
        group: item.group,
        status: item.status,
        summary: item.summary,
      })),
      nextSteps: setupHealth.nextSteps,
      readiness: setupHealth.readiness,
      score: setupHealth.score,
      totals: setupHealth.totals,
    },
    state: redactDiagnosticValue(state),
  };

  return YAML.stringify(bundle, {
    simpleKeys: true,
    sortMapEntries: false,
  });
};
