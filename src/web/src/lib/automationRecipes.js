import {
  getLocalStorageItem,
  removeLocalStorageItem,
  setLocalStorageItem,
} from './storage';

const storageKey = 'slskdn.automationRecipeState';

export const automationRecipes = [
  {
    cadence: 'Continuous',
    description: 'Checks connection, shares, paths, and credentials for setup drift.',
    enabledByDefault: true,
    fileImpact: 'Read only',
    icon: 'stethoscope',
    id: 'local-diagnostics',
    networkImpact: 'Local',
    title: 'Local Diagnostics',
  },
  {
    cadence: 'Daily',
    description: 'Surfaces stale share-cache and library-scan reminders before users hit missing results.',
    enabledByDefault: true,
    fileImpact: 'Read only',
    icon: 'bell outline',
    id: 'stale-cache-reminders',
    networkImpact: 'Local',
    title: 'Share and Library Reminders',
  },
  {
    cadence: 'Every 15 minutes',
    description: 'Keeps local dashboard summaries fresh without contacting public peers.',
    enabledByDefault: true,
    fileImpact: 'Read only',
    icon: 'dashboard',
    id: 'dashboard-refresh',
    networkImpact: 'Local',
    title: 'Dashboard Refresh',
  },
  {
    cadence: 'Manual or scheduled',
    description: 'Scans watched artists, labels, and scenes for likely missing releases.',
    enabledByDefault: false,
    fileImpact: 'None until approved',
    icon: 'eye',
    id: 'watchlist-scan',
    networkImpact: 'Metadata providers',
    title: 'Watchlist Scan',
  },
  {
    cadence: 'Weekly',
    description: 'Promotes newly discovered artist releases into the Discovery Inbox.',
    enabledByDefault: false,
    fileImpact: 'Inbox only',
    icon: 'calendar plus',
    id: 'release-radar',
    networkImpact: 'Metadata providers',
    title: 'Release Radar',
  },
  {
    cadence: 'Manual or scheduled',
    description: 'Refreshes mirrored playlists and queues changed tracks for review.',
    enabledByDefault: false,
    fileImpact: 'Inbox only',
    icon: 'sync alternate',
    id: 'playlist-refresh',
    networkImpact: 'Configured providers',
    title: 'Playlist Refresh',
  },
  {
    cadence: 'Manual or scheduled',
    description: 'Retries failed Wishlist items using the selected acquisition profile.',
    enabledByDefault: false,
    fileImpact: 'Downloads after approval',
    icon: 'redo',
    id: 'wishlist-retry',
    networkImpact: 'Public peers possible',
    title: 'Wishlist Retry',
  },
  {
    cadence: 'Manual or scheduled',
    description: 'Scans completed downloads into staging for match, tag, and import review.',
    enabledByDefault: false,
    fileImpact: 'Read only',
    icon: 'inbox',
    id: 'import-staging-scan',
    networkImpact: 'Local',
    title: 'Import Staging Scan',
  },
  {
    cadence: 'Manual or scheduled',
    description: 'Finds duplicates, dead files, metadata gaps, fake lossless files, and missing art.',
    enabledByDefault: false,
    fileImpact: 'Read only until fixed',
    icon: 'heartbeat',
    id: 'library-health-scan',
    networkImpact: 'Local',
    title: 'Library Health Scan',
  },
  {
    cadence: 'After import',
    description: 'Asks configured media servers to rescan after successful library imports.',
    enabledByDefault: false,
    fileImpact: 'Media-server scan',
    icon: 'server',
    id: 'media-server-rescan',
    networkImpact: 'Local network',
    title: 'Media Server Rescan',
  },
  {
    cadence: 'Manual or scheduled',
    description: 'Publishes explicit opt-in signed quality and verification evidence to trusted mesh peers.',
    enabledByDefault: false,
    fileImpact: 'No file writes',
    icon: 'share alternate',
    id: 'mesh-evidence-publish',
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
  const nextState = {
    ...state,
    [id]: {
      ...recipeState,
      lastDryRunAt: timestamp,
    },
  };

  setLocalStorageItem(storageKey, JSON.stringify(nextState));
  return nextState;
};

export const automationRecipeStorageKey = storageKey;
