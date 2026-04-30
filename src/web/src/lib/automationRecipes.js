import {
  getLocalStorageItem,
  removeLocalStorageItem,
  setLocalStorageItem,
} from './storage';

const storageKey = 'slskdn.automationRecipeState';

export const automationRecipes = [
  {
    approvalGate: 'None required',
    cadence: 'Continuous',
    cooldown: '5 minutes',
    description: 'Checks connection, shares, paths, and credentials for setup drift.',
    enabledByDefault: true,
    fileImpact: 'Read only',
    icon: 'stethoscope',
    id: 'local-diagnostics',
    maxRunTime: '30 seconds',
    networkImpact: 'Local',
    title: 'Local Diagnostics',
  },
  {
    approvalGate: 'None required',
    cadence: 'Daily',
    cooldown: '24 hours',
    description: 'Surfaces stale share-cache and library-scan reminders before users hit missing results.',
    enabledByDefault: true,
    fileImpact: 'Read only',
    icon: 'bell outline',
    id: 'stale-cache-reminders',
    maxRunTime: '1 minute',
    networkImpact: 'Local',
    title: 'Share and Library Reminders',
  },
  {
    approvalGate: 'None required',
    cadence: 'Every 15 minutes',
    cooldown: '15 minutes',
    description: 'Keeps local dashboard summaries fresh without contacting public peers.',
    enabledByDefault: true,
    fileImpact: 'Read only',
    icon: 'dashboard',
    id: 'dashboard-refresh',
    maxRunTime: '20 seconds',
    networkImpact: 'Local',
    title: 'Dashboard Refresh',
  },
  {
    approvalGate: 'Discovery Inbox approval',
    cadence: 'Manual or scheduled',
    cooldown: '6 hours',
    description: 'Scans watched artists, labels, and scenes for likely missing releases.',
    enabledByDefault: false,
    fileImpact: 'None until approved',
    icon: 'eye',
    id: 'watchlist-scan',
    maxRunTime: '10 minutes',
    networkImpact: 'Metadata providers',
    title: 'Watchlist Scan',
  },
  {
    approvalGate: 'Discovery Inbox approval',
    cadence: 'Weekly',
    cooldown: '7 days',
    description: 'Promotes newly discovered artist releases into the Discovery Inbox.',
    enabledByDefault: false,
    fileImpact: 'Inbox only',
    icon: 'calendar plus',
    id: 'release-radar',
    maxRunTime: '15 minutes',
    networkImpact: 'Metadata providers',
    title: 'Release Radar',
  },
  {
    approvalGate: 'Discovery Inbox approval',
    cadence: 'Manual or scheduled',
    cooldown: '1 hour',
    description: 'Refreshes mirrored playlists and queues changed tracks for review.',
    enabledByDefault: false,
    fileImpact: 'Inbox only',
    icon: 'sync alternate',
    id: 'playlist-refresh',
    maxRunTime: '10 minutes',
    networkImpact: 'Configured providers',
    title: 'Playlist Refresh',
  },
  {
    approvalGate: 'Download approval',
    cadence: 'Manual or scheduled',
    cooldown: '2 hours',
    description: 'Retries failed Wishlist items using the selected acquisition profile.',
    enabledByDefault: false,
    fileImpact: 'Downloads after approval',
    icon: 'redo',
    id: 'wishlist-retry',
    maxRunTime: '20 minutes',
    networkImpact: 'Public peers possible',
    title: 'Wishlist Retry',
  },
  {
    approvalGate: 'Import review',
    cadence: 'Manual or scheduled',
    cooldown: '30 minutes',
    description: 'Scans completed downloads into staging for match, tag, and import review.',
    enabledByDefault: false,
    fileImpact: 'Read only',
    icon: 'inbox',
    id: 'import-staging-scan',
    maxRunTime: '5 minutes',
    networkImpact: 'Local',
    title: 'Import Staging Scan',
  },
  {
    approvalGate: 'Fix confirmation',
    cadence: 'Manual or scheduled',
    cooldown: '24 hours',
    description: 'Finds duplicates, dead files, metadata gaps, fake lossless files, and missing art.',
    enabledByDefault: false,
    fileImpact: 'Read only until fixed',
    icon: 'heartbeat',
    id: 'library-health-scan',
    maxRunTime: '30 minutes',
    networkImpact: 'Local',
    title: 'Library Health Scan',
  },
  {
    approvalGate: 'Configured import success',
    cadence: 'After import',
    cooldown: '10 minutes',
    description: 'Asks configured media servers to rescan after successful library imports.',
    enabledByDefault: false,
    fileImpact: 'Media-server scan',
    icon: 'server',
    id: 'media-server-rescan',
    maxRunTime: '2 minutes',
    networkImpact: 'Local network',
    title: 'Media Server Rescan',
  },
  {
    approvalGate: 'Explicit evidence publication opt-in',
    cadence: 'Manual or scheduled',
    cooldown: '12 hours',
    description: 'Publishes explicit opt-in signed quality and verification evidence to trusted mesh peers.',
    enabledByDefault: false,
    fileImpact: 'No file writes',
    icon: 'share alternate',
    id: 'mesh-evidence-publish',
    maxRunTime: '10 minutes',
    networkImpact: 'Trusted mesh',
    title: 'Mesh Evidence Publish',
  },
];

const defaultState = automationRecipes.reduce((state, recipe) => {
  state[recipe.id] = {
    enabled: recipe.enabledByDefault,
    lastDryRunAt: null,
  };
  return state;
}, {});

const readStoredState = () => {
  const stored = getLocalStorageItem(storageKey);
  if (!stored) {
    return {};
  }

  try {
    return JSON.parse(stored);
  } catch {
    removeLocalStorageItem(storageKey);
    return {};
  }
};

export const buildAutomationDryRunReport = (
  recipe,
  timestamp = new Date().toISOString(),
) => ({
  approvalGate: recipe.approvalGate,
  cooldown: recipe.cooldown,
  executed: false,
  fileImpact: recipe.fileImpact,
  generatedAt: timestamp,
  maxRunTime: recipe.maxRunTime,
  networkImpact: recipe.networkImpact,
  recipeId: recipe.id,
  title: recipe.title,
});

export const getAutomationRecipeState = () => ({
  ...defaultState,
  ...readStoredState(),
});

export const setAutomationRecipeEnabled = (id, enabled) => {
  const state = getAutomationRecipeState();
  const recipeState = state[id] ?? {};
  const nextState = {
    ...state,
    [id]: {
      ...recipeState,
      enabled,
    },
  };

  setLocalStorageItem(storageKey, JSON.stringify(nextState));
  return nextState;
};

export const setAutomationRecipeDryRun = (id, timestamp = new Date().toISOString()) => {
  const state = getAutomationRecipeState();
  const recipeState = state[id] ?? {};
  const recipe = automationRecipes.find((item) => item.id === id);
  const nextState = {
    ...state,
    [id]: {
      ...recipeState,
      lastDryRunAt: timestamp,
      lastDryRunReport: recipe
        ? buildAutomationDryRunReport(recipe, timestamp)
        : undefined,
    },
  };

  setLocalStorageItem(storageKey, JSON.stringify(nextState));
  return nextState;
};

export const automationRecipeStorageKey = storageKey;
