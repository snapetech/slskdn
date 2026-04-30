export const communityQualitySignalStorageKey = 'slskdn.communityQualitySignals';

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

const writeSignals = (signals) => {
  const storage = getStorage();
  if (!storage) return signals;

  storage.setItem(communityQualitySignalStorageKey, JSON.stringify(signals));
  return signals;
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

export const getCommunityQualitySummary = (username) => {
  const normalizedUsername = normalizeUsername(username);
  const signals = getCommunityQualitySignals().filter(
    (signal) => signal.username === normalizedUsername,
  );
  const positive = signals.filter((signal) => signal.category === 'positive').length;
  const negative = signals.filter((signal) => signal.category === 'negative').length;
  const score = Math.min(Math.max((positive * 4) - (negative * 6), -18), 18);

  return {
    latestReason: signals[signals.length - 1]?.reason || '',
    negative,
    positive,
    score,
    signals,
    username: normalizedUsername,
  };
};

export const getCommunityQualityLabel = (summary) => {
  if (!summary || summary.signals.length === 0) {
    return null;
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
