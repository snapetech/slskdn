export const communityQualitySignalStorageKey = 'slskdn.communityQualitySignals';
export const communityQualityOverrideStorageKey =
  'slskdn.communityQualityOverrides';

const positiveSignalTypes = new Set([
  'served-verified-content',
  'queue-reliable',
  'completed-album-consistent',
]);

const negativeSignalTypes = new Set([
  'failed-verification',
  'queue-unreliable',
  'suspicious-candidate',
]);

const normalizeUsername = (username = '') => username.trim();

const getStorage = () => {
  try {
    return window.localStorage;
  } catch (_error) {
    return null;
  }
};

const readSignals = () => {
  const storage = getStorage();
  if (!storage) return [];

  try {
    const parsed = JSON.parse(
      storage.getItem(communityQualitySignalStorageKey) || '[]',
    );
    return Array.isArray(parsed) ? parsed : [];
  } catch (_error) {
    return [];
  }
};

const readOverrides = () => {
  const storage = getStorage();
  if (!storage) return {};

  try {
    const parsed = JSON.parse(
      storage.getItem(communityQualityOverrideStorageKey) || '{}',
    );
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed)
      ? parsed
      : {};
  } catch (_error) {
    return {};
  }
};

const writeSignals = (signals) => {
  const storage = getStorage();
  if (!storage) return signals;

  storage.setItem(communityQualitySignalStorageKey, JSON.stringify(signals));
  return signals;
};

const writeOverrides = (overrides) => {
  const storage = getStorage();
  if (!storage) return overrides;

  storage.setItem(communityQualityOverrideStorageKey, JSON.stringify(overrides));
  return overrides;
};

const normalizeSignal = (signal) => {
  const username = normalizeUsername(signal.username);
  const type = signal.type || 'suspicious-candidate';
  const category = positiveSignalTypes.has(type)
    ? 'positive'
    : negativeSignalTypes.has(type)
      ? 'negative'
      : 'neutral';

  return {
    category,
    createdAt: signal.createdAt || new Date().toISOString(),
    id:
      signal.id ||
      `quality-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    reason: (signal.reason || '').trim(),
    source: signal.source || 'local-review',
    type,
    username,
  };
};

export const getCommunityQualitySignals = () => readSignals();

export const saveCommunityQualitySignals = (signals) =>
  writeSignals(
    signals
      .map(normalizeSignal)
      .filter((signal) => signal.username)
      .slice(-500),
  );

export const recordCommunityQualitySignal = (signal) => {
  const normalized = normalizeSignal(signal);
  if (!normalized.username) {
    return getCommunityQualitySignals();
  }

  return saveCommunityQualitySignals([...getCommunityQualitySignals(), normalized]);
};

export const clearCommunityQualitySignalsForUser = (username) => {
  const normalizedUsername = normalizeUsername(username);
  return saveCommunityQualitySignals(
    getCommunityQualitySignals().filter(
      (signal) => signal.username !== normalizedUsername,
    ),
  );
};

const normalizeOverrideMode = (mode) =>
  ['ignore', 'trust', 'caution'].includes(mode) ? mode : 'ignore';

const normalizeOverride = (override = {}) => ({
  createdAt: override.createdAt || new Date().toISOString(),
  mode: normalizeOverrideMode(override.mode),
  note: (override.note || '').trim(),
  source: override.source || 'local-review',
});

export const getCommunityQualityOverrides = () => readOverrides();

export const setCommunityQualityOverride = (username, override = {}) => {
  const normalizedUsername = normalizeUsername(username);
  if (!normalizedUsername) {
    return getCommunityQualityOverrides();
  }

  return writeOverrides({
    ...getCommunityQualityOverrides(),
    [normalizedUsername]: normalizeOverride(override),
  });
};

export const clearCommunityQualityOverride = (username) => {
  const normalizedUsername = normalizeUsername(username);
  const overrides = { ...getCommunityQualityOverrides() };
  delete overrides[normalizedUsername];
  return writeOverrides(overrides);
};

export const getCommunityQualitySummary = (username) => {
  const normalizedUsername = normalizeUsername(username);
  const signals = getCommunityQualitySignals().filter(
    (signal) => signal.username === normalizedUsername,
  );
  const positive = signals.filter((signal) => signal.category === 'positive').length;
  const negative = signals.filter((signal) => signal.category === 'negative').length;
  const override = getCommunityQualityOverrides()[normalizedUsername] || null;
  const rawScore = Math.min(Math.max((positive * 4) - (negative * 6), -18), 18);
  const score =
    override?.mode === 'ignore'
      ? 0
      : override?.mode === 'trust'
        ? Math.max(rawScore, 8)
        : override?.mode === 'caution'
          ? Math.min(rawScore, -6)
          : rawScore;

  return {
    latestReason: signals[signals.length - 1]?.reason || '',
    negative,
    override,
    positive,
    rawScore,
    score,
    signals,
    username: normalizedUsername,
  };
};

export const getCommunityQualityLabel = (summary) => {
  if (!summary || summary.signals.length === 0) {
    if (!summary?.override) return null;

    return {
      color: summary.override.mode === 'trust' ? 'green' : 'grey',
      icon: summary.override.mode === 'trust' ? 'shield alternate' : 'eye slash',
      text:
        summary.override.mode === 'trust'
          ? 'Local trust override'
          : 'Signals ignored',
    };
  }

  if (summary.override?.mode === 'ignore') {
    return {
      color: 'grey',
      icon: 'eye slash',
      text: 'Signals ignored',
    };
  }

  if (summary.override?.mode === 'trust') {
    return {
      color: 'green',
      icon: 'shield alternate',
      text: 'Local trust override',
    };
  }

  if (summary.override?.mode === 'caution') {
    return {
      color: 'orange',
      icon: 'exclamation triangle',
      text: 'Local caution override',
    };
  }

  if (summary.score >= 8) {
    return {
      color: 'green',
      icon: 'shield alternate',
      text: 'Local trust',
    };
  }

  if (summary.score <= -6) {
    return {
      color: 'orange',
      icon: 'exclamation triangle',
      text: 'Local caution',
    };
  }

  return {
    color: 'violet',
    icon: 'balance scale',
    text: 'Local signals',
  };
};
