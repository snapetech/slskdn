import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Button, Icon, Popup } from 'semantic-ui-react';
import { resumeAudioGraph } from './audioGraph';
import SpectrumAnalyzer from './SpectrumAnalyzer';
import { createButterchurnEngine } from './visualizers/butterchurnEngine';
import { createNativeMilkdropEngine } from './visualizers/nativeMilkdropEngine';

const visualizerEngineStorageKey = 'slskdn.player.visualizerEngine';
const nativePresetStorageKey = 'slskdn.player.nativeMilkdropPreset';
const nativePresetLibraryStorageKey = 'slskdn.player.nativeMilkdropPresetLibrary';
const nativePresetAutomationStorageKey = 'slskdn.player.nativeMilkdropPresetAutomation';
const nativePresetFavoritesStorageKey = 'slskdn.player.nativeMilkdropPresetFavorites';
const nativePresetLibraryModeStorageKey = 'slskdn.player.nativeMilkdropPresetLibraryMode';
const nativePresetSearchStorageKey = 'slskdn.player.nativeMilkdropPresetSearch';
const nativePresetLibraryLimit = 20;
const nativePresetHistoryLimit = 12;
const nativeTextureAssetMaxBytes = 1024 * 1024;

const readStoredEngine = () => {
  if (typeof window === 'undefined') return 'butterchurn';
  return window.localStorage.getItem(visualizerEngineStorageKey) === 'native'
    ? 'native'
    : 'butterchurn';
};

const getNextEngine = (engine) => (engine === 'native' ? 'butterchurn' : 'native');

const getEngineLabel = (engine) => (engine === 'native' ? 'slskdN native' : 'Butterchurn');

const getNextNativeAutomationMode = (mode) => {
  if (mode === 'off') return 'beat';
  if (mode === 'beat') return 'timed';
  return 'off';
};

const getNativeAutomationLabel = (mode) => {
  if (mode === 'beat') return 'Beat';
  if (mode === 'timed') return 'Timed';
  return 'Off';
};

const readStoredNativeAutomationMode = () => {
  if (typeof window === 'undefined') return 'off';
  const mode = window.localStorage.getItem(nativePresetAutomationStorageKey);
  return ['beat', 'timed'].includes(mode) ? mode : 'off';
};

const getVisualizerErrorMessage = (engineType, error) => {
  const detail = error?.message ? ` ${error.message}` : '';
  return engineType === 'native'
    ? `Native MilkDrop render failed.${detail}`
    : 'MilkDrop failed. Showing analyzer fallback.';
};

const readStoredNativePreset = () => {
  if (typeof window === 'undefined') return null;
  try {
    return JSON.parse(window.localStorage.getItem(nativePresetStorageKey) || 'null');
  } catch {
    return null;
  }
};

const readStoredNativePresetLibrary = () => {
  if (typeof window === 'undefined') return [];
  try {
    const library = JSON.parse(
      window.localStorage.getItem(nativePresetLibraryStorageKey) || '[]',
    );
    return Array.isArray(library)
      ? library.filter((preset) => preset?.id && preset?.source)
      : [];
  } catch {
    return [];
  }
};

const readStoredNativePresetFavorites = () => {
  if (typeof window === 'undefined') return [];
  try {
    const favorites = JSON.parse(
      window.localStorage.getItem(nativePresetFavoritesStorageKey) || '[]',
    );
    return Array.isArray(favorites)
      ? favorites.filter((id) => typeof id === 'string' && id.length > 0)
      : [];
  } catch {
    return [];
  }
};

const readStoredNativePresetLibraryMode = () => {
  if (typeof window === 'undefined') return 'all';
  return window.localStorage.getItem(nativePresetLibraryModeStorageKey) === 'favorites'
    ? 'favorites'
    : 'all';
};

const readStoredNativePresetSearch = () => {
  if (typeof window === 'undefined') return '';
  return window.localStorage.getItem(nativePresetSearchStorageKey) || '';
};

const writeStoredNativePresetLibrary = (library) => {
  window.localStorage.setItem(
    nativePresetLibraryStorageKey,
    JSON.stringify(library.slice(0, nativePresetLibraryLimit)),
  );
};

const writeStoredNativePresetFavorites = (favoriteIds) => {
  if (favoriteIds.length === 0) {
    window.localStorage.removeItem(nativePresetFavoritesStorageKey);
    return;
  }
  window.localStorage.setItem(
    nativePresetFavoritesStorageKey,
    JSON.stringify(favoriteIds),
  );
};

const upsertNativePresetLibraryEntry = (library, entry) => [
  entry,
  ...library.filter((preset) => preset.id !== entry.id),
].slice(0, nativePresetLibraryLimit);

const pruneNativePresetFavorites = (favoriteIds, library) => {
  const libraryIds = new Set(library.map((preset) => preset.id));
  return favoriteIds.filter((id) => libraryIds.has(id));
};

const getNativePresetSearchText = (preset) =>
  [preset.title, preset.fileName].filter(Boolean).join(' ').toLowerCase();

const filterNativePresetLibrary = (library, search) => {
  const query = search.trim().toLowerCase();
  if (!query) return library;
  const terms = query.split(/\s+/).filter(Boolean);
  return library.filter((preset) => {
    const text = getNativePresetSearchText(preset);
    return terms.every((term) => text.includes(term));
  });
};

const getNativePresetFileId = (file) =>
  [file.name, file.size, file.lastModified].filter((part) => part !== undefined).join(':');

const isNativePresetFile = (file) => /\.(milk2?|txt)$/i.test(file.name);

const isNativeFragmentFile = (file) => /\.(shape|wave)$/i.test(file.name);

const getNativeImportFilePath = (file) =>
  file.webkitRelativePath || file.name;

const isNativeTextureAssetCandidateFile = (file) =>
  /^image\//i.test(file.type) || /\.(png|jpe?g|webp|gif)$/i.test(file.name);

const getNativeTextureAssetSkip = (file) => {
  if (isNativePresetFile(file) || isNativeFragmentFile(file)) return null;
  if (!isNativeTextureAssetCandidateFile(file)) {
    return {
      fileName: file.name,
      message: 'Unsupported file type.',
    };
  }
  if (file.size > nativeTextureAssetMaxBytes) {
    return {
      fileName: file.name,
      message: 'Texture asset is larger than 1 MB.',
    };
  }
  return null;
};

const getTextureAssetKeys = (fileName) => {
  const normalized = fileName.trim().replace(/^['"]|['"]$/g, '').replace(/\\/g, '/').toLowerCase();
  const basename = normalized.replace(/^.*[\\/]/, '');
  const stem = basename.replace(/\.[^.]+$/, '');
  return Array.from(new Set([normalized, basename, stem].filter(Boolean)));
};

const textureReferencePattern =
  /(?:shape|sprite)\d+_(?:texture|tex|tex_name|image|img|file|filename)\s*=\s*([^\r\n;]+)/gi;
const standaloneTextureReferencePattern =
  /^\s*(?:texture|tex|tex_name|image|img|file|filename)\s*=\s*([^\r\n;]+)/gim;

const collectNativePresetTextureReferences = (source) => {
  const references = new Set();
  let match = textureReferencePattern.exec(source || '');
  while (match) {
    getTextureAssetKeys(match[1]).forEach((key) => references.add(key));
    match = textureReferencePattern.exec(source || '');
  }
  match = standaloneTextureReferencePattern.exec(source || '');
  while (match) {
    getTextureAssetKeys(match[1]).forEach((key) => references.add(key));
    match = standaloneTextureReferencePattern.exec(source || '');
  }
  return references;
};

const selectNativePresetTextureAssets = (source, textureAssets) => {
  const references = collectNativePresetTextureReferences(source);
  if (references.size === 0) return {};
  const selected = {};
  Object.entries(textureAssets).forEach(([key, asset]) => {
    if (!references.has(key)) return;
    getTextureAssetKeys(asset.fileName).forEach((alias) => {
      selected[alias] = asset;
    });
  });
  return selected;
};

const readFileAsDataUrl = (file) => new Promise((resolve, reject) => {
  if (typeof FileReader !== 'function') {
    reject(new Error('Texture asset imports require FileReader support.'));
    return;
  }
  const reader = new FileReader();
  reader.onerror = () => reject(reader.error || new Error(`Failed to read ${file.name}.`));
  reader.onload = () => resolve(reader.result);
  reader.readAsDataURL(file);
});

const readNativeTextureAssets = async (files) => {
  const textureAssets = {};
  const skippedTextureAssets = [];
  for (const file of files.filter((entry) =>
    !isNativePresetFile(entry) && !isNativeFragmentFile(entry))) {
    const skip = getNativeTextureAssetSkip(file);
    if (skip) {
      skippedTextureAssets.push(skip);
      continue;
    }
    let dataUrl = null;
    try {
      dataUrl = await readFileAsDataUrl(file);
    } catch (textureError) {
      skippedTextureAssets.push({
        fileName: file.name,
        message: textureError?.message || 'Texture asset could not be read.',
      });
      continue;
    }
    const filePath = getNativeImportFilePath(file);
    getTextureAssetKeys(filePath).forEach((key) => {
      textureAssets[key] = {
        dataUrl,
        fileName: filePath,
      };
    });
  }
  return { skippedTextureAssets, textureAssets };
};

const downloadTextFile = (fileName, source) => {
  const blob = new Blob([source], { type: 'text/plain' });
  const url = window.URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(url);
};

const formatSkippedFileNames = (skipped) => {
  const skippedNames = skipped.slice(0, 3).map((entry) => entry.fileName).join(', ');
  const remaining = skipped.length > 3 ? `, +${skipped.length - 3} more` : '';
  return `${skippedNames}${remaining}`;
};

const getNativePresetImportMessage = ({ importedCount, skipped, skippedTextureAssets }) => {
  const messages = [];
  if (skipped.length > 0) {
    const prefix = importedCount > 0
      ? `Imported ${importedCount}; skipped ${skipped.length}`
      : `Native preset import failed for ${skipped.length}`;
    messages.push(`${prefix}: ${formatSkippedFileNames(skipped)}.`);
  }
  if (skippedTextureAssets.length > 0) {
    const noun = skippedTextureAssets.length === 1 ? 'texture asset' : 'texture assets';
    messages.push(
      `Skipped ${skippedTextureAssets.length} ${noun}: ${formatSkippedFileNames(skippedTextureAssets)}.`,
    );
  }
  return messages.length > 0 ? messages.join(' ') : null;
};

const supportsWebGl2 = () => {
  try {
    const canvas = document.createElement('canvas');
    return Boolean(canvas.getContext('webgl2'));
  } catch {
    return false;
  }
};

const Visualizer = ({ audioElement, mode, onModeChange }) => {
  const containerRef = useRef(null);
  const canvasRef = useRef(null);
  const directoryInputRef = useRef(null);
  const engineRef = useRef(null);
  const fileInputRef = useRef(null);
  const rafRef = useRef(null);
  const engineAudioNodeRef = useRef(null);
  const nativeAutomationModeRef = useRef(readStoredNativeAutomationMode());
  const [fallbackMode, setFallbackMode] = useState(false);
  const [engineType, setEngineType] = useState(readStoredEngine);
  const [engineName, setEngineName] = useState('');
  const [nativeAutomationMode, setNativeAutomationMode] = useState(
    () => nativeAutomationModeRef.current,
  );
  const [activeNativePresetId, setActiveNativePresetId] = useState(
    () => readStoredNativePreset()?.id || '',
  );
  const [nativeFavoritePresetIds, setNativeFavoritePresetIds] = useState(
    readStoredNativePresetFavorites,
  );
  const [nativeLibraryMode, setNativeLibraryMode] = useState(
    readStoredNativePresetLibraryMode,
  );
  const [nativePresetHistory, setNativePresetHistory] = useState([]);
  const [nativePresetLibrary, setNativePresetLibrary] = useState(readStoredNativePresetLibrary);
  const [nativePresetSearch, setNativePresetSearch] = useState(readStoredNativePresetSearch);
  const [presetName, setPresetName] = useState('');
  const [error, setError] = useState(null);

  const modeFilteredNativePresetLibrary = nativeLibraryMode === 'favorites'
    ? nativePresetLibrary.filter((preset) => nativeFavoritePresetIds.includes(preset.id))
    : nativePresetLibrary;
  const visibleNativePresetLibrary = filterNativePresetLibrary(
    modeFilteredNativePresetLibrary,
    nativePresetSearch,
  );
  const visibleNativePresetIndex = visibleNativePresetLibrary.findIndex(
    (preset) => preset.id === activeNativePresetId,
  );
  const activeNativePresetIsFavorite = nativeFavoritePresetIds.includes(activeNativePresetId);
  const selectedNativePresetValue = visibleNativePresetLibrary.some(
    (preset) => preset.id === activeNativePresetId,
  )
    ? activeNativePresetId
    : '';
  const hasNativePresetSearch = nativePresetSearch.trim().length > 0;
  const nativeBankNavigationDisabled = engineType === 'native'
    && nativePresetLibrary.length > 0
    && visibleNativePresetLibrary.length === 0;

  const renderLoop = useCallback(() => {
    if (!engineRef.current) return;
    try {
      const renderResult = engineRef.current.render();
      if (renderResult?.presetName) {
        setPresetName(renderResult.presetName);
      }
    } catch (renderError) {
      // eslint-disable-next-line no-console
      console.error('Failed to render MilkDrop visualizer', renderError);
      if (engineType === 'native') {
        window.localStorage.removeItem(nativePresetStorageKey);
      }
      setError(getVisualizerErrorMessage(engineType, renderError));
      return;
    }
    rafRef.current = window.requestAnimationFrame(renderLoop);
  }, [engineType]);

  const cycleNativeAutomationMode = useCallback(() => {
    setNativeAutomationMode((current) => getNextNativeAutomationMode(current));
  }, []);

  const sizeCanvas = useCallback(() => {
    const container = containerRef.current;
    const canvas = canvasRef.current;
    const engine = engineRef.current;
    if (!container || !canvas || !engine) return;
    const rect = container.getBoundingClientRect();
    const width = Math.max(1, Math.floor(rect.width));
    const height = Math.max(1, Math.floor(rect.height));
    canvas.width = width;
    canvas.height = height;
    engine.resize(width, height);
  }, []);

  const loadNativePresetEntry = useCallback((preset, options = {}) => {
    if (!preset || !engineRef.current?.loadPresetText) return false;
    const { pushHistory = true } = options;

    try {
      setError(null);
      const loadedPresetName = engineRef.current.loadPresetText(
        preset.source,
        preset.fileName,
        { textureAssets: preset.textureAssets },
      );
      window.localStorage.setItem(nativePresetStorageKey, JSON.stringify(preset));
      if (pushHistory && activeNativePresetId && activeNativePresetId !== preset.id) {
        setNativePresetHistory((history) => [
          activeNativePresetId,
          ...history.filter((id) => id !== activeNativePresetId && id !== preset.id),
        ].slice(0, nativePresetHistoryLimit));
      }
      setActiveNativePresetId(preset.id);
      setPresetName(loadedPresetName);
      sizeCanvas();
      return true;
    } catch (presetError) {
      // eslint-disable-next-line no-console
      console.error('Failed to load native MilkDrop preset from library', presetError);
      setError(presetError?.message || 'Native preset load failed.');
      return false;
    }
  }, [activeNativePresetId, sizeCanvas]);

  const loadNativePresetByOffset = useCallback((offset) => {
    if (visibleNativePresetLibrary.length === 0) return false;
    const currentIndex = visibleNativePresetIndex >= 0
      ? visibleNativePresetIndex
      : (offset > 0 ? -1 : 0);
    const nextIndex = (
      currentIndex + offset + visibleNativePresetLibrary.length
    ) % visibleNativePresetLibrary.length;
    return loadNativePresetEntry(visibleNativePresetLibrary[nextIndex]);
  }, [loadNativePresetEntry, visibleNativePresetIndex, visibleNativePresetLibrary]);

  const cyclePreset = useCallback(() => {
    if (engineType === 'native' && nativePresetLibrary.length > 0) {
      loadNativePresetByOffset(1);
      return;
    }
    if (!engineRef.current) return;
    const nextPresetName = engineRef.current.nextPreset();
    if (nextPresetName) {
      setPresetName(nextPresetName);
    }
  }, [engineType, loadNativePresetByOffset, nativePresetLibrary.length]);

  const previousNativeLibraryPreset = useCallback(() => {
    if (nativePresetHistory.length > 0) {
      const [previousId, ...remainingHistory] = nativePresetHistory;
      const previousPreset = nativePresetLibrary.find((preset) => preset.id === previousId);
      setNativePresetHistory(remainingHistory);
      loadNativePresetEntry(previousPreset, { pushHistory: false });
      return;
    }
    loadNativePresetByOffset(-1);
  }, [
    loadNativePresetByOffset,
    loadNativePresetEntry,
    nativePresetHistory,
    nativePresetLibrary,
  ]);

  const randomNativeLibraryPreset = useCallback(() => {
    if (visibleNativePresetLibrary.length === 0) return;
    const candidates = visibleNativePresetLibrary.filter(
      (preset) => preset.id !== activeNativePresetId,
    );
    const pool = candidates.length > 0 ? candidates : visibleNativePresetLibrary;
    const randomIndex = Math.floor(Math.random() * pool.length);
    loadNativePresetEntry(pool[randomIndex]);
  }, [activeNativePresetId, loadNativePresetEntry, visibleNativePresetLibrary]);

  const toggleNativePresetFavorite = useCallback(() => {
    if (!activeNativePresetId) return;
    setNativeFavoritePresetIds((favoriteIds) => {
      const nextFavoriteIds = favoriteIds.includes(activeNativePresetId)
        ? favoriteIds.filter((id) => id !== activeNativePresetId)
        : [activeNativePresetId, ...favoriteIds];
      writeStoredNativePresetFavorites(nextFavoriteIds);
      return nextFavoriteIds;
    });
  }, [activeNativePresetId]);

  const toggleNativeLibraryMode = useCallback(() => {
    setNativeLibraryMode((current) => {
      const nextMode = current === 'favorites' ? 'all' : 'favorites';
      window.localStorage.setItem(nativePresetLibraryModeStorageKey, nextMode);
      return nextMode;
    });
  }, []);

  const updateNativePresetSearch = useCallback((event) => {
    const nextSearch = event.target.value;
    setNativePresetSearch(nextSearch);
    if (nextSearch.trim()) {
      window.localStorage.setItem(nativePresetSearchStorageKey, nextSearch);
    } else {
      window.localStorage.removeItem(nativePresetSearchStorageKey);
    }
  }, []);

  const clearNativePresetSearch = useCallback(() => {
    setNativePresetSearch('');
    window.localStorage.removeItem(nativePresetSearchStorageKey);
  }, []);

  useEffect(() => {
    if (mode === 'off' || !audioElement || !canvasRef.current) return undefined;

    let cancelled = false;
    let resizeObserver = null;

    (async () => {
      try {
        setError(null);
        setFallbackMode(false);
        const graph = await resumeAudioGraph(audioElement);
        if (!graph) {
          setError('Web Audio is not available in this browser.');
          setFallbackMode(true);
          return;
        }

        if (!supportsWebGl2()) {
          setError('MilkDrop needs WebGL2. Showing analyzer fallback.');
          setFallbackMode(true);
          return;
        }

        const createEngine = engineType === 'native'
          ? createNativeMilkdropEngine
          : createButterchurnEngine;
        const engine = await createEngine({
          audioContext: graph.ctx,
          audioNode: graph.visualizerInput,
          canvas: canvasRef.current,
          pixelRatio: window.devicePixelRatio || 1,
        });
        if (cancelled) {
          engine.dispose();
          return;
        }

        engineRef.current = engine;
        engineAudioNodeRef.current = graph.visualizerInput;
        setEngineName(engine.name);
        setPresetName(engine.presetName);
        if (engineType === 'native' && engine.setPresetAutomation) {
          engine.setPresetAutomation({ mode: nativeAutomationModeRef.current });
        }
        const storedNativePreset = engineType === 'native' ? readStoredNativePreset() : null;
        if (storedNativePreset?.source && engine.loadPresetText) {
          const importedPresetName = engine.loadPresetText(
            storedNativePreset.source,
            storedNativePreset.fileName,
            { textureAssets: storedNativePreset.textureAssets },
          );
          setActiveNativePresetId(storedNativePreset.id || '');
          setPresetName(importedPresetName);
        }
        sizeCanvas();

        if (typeof window.ResizeObserver === 'function' && containerRef.current) {
          resizeObserver = new window.ResizeObserver(() => sizeCanvas());
          resizeObserver.observe(containerRef.current);
        }

        rafRef.current = window.requestAnimationFrame(renderLoop);
      } catch (importError) {
        // eslint-disable-next-line no-console
        console.error('Failed to load Milkdrop visualizer', importError);
        setError(getVisualizerErrorMessage(engineType, importError));
        setFallbackMode(true);
      }
    })();

    return () => {
      cancelled = true;
      if (rafRef.current) {
        window.cancelAnimationFrame(rafRef.current);
        rafRef.current = null;
      }
      if (resizeObserver) {
        resizeObserver.disconnect();
      }
      if (engineRef.current && engineAudioNodeRef.current) {
        try {
          engineRef.current.dispose();
        } catch {
          // The engine may already have disconnected during canvas teardown.
        }
      }
      engineRef.current = null;
      engineAudioNodeRef.current = null;
      setEngineName('');
    };
  }, [mode, audioElement, engineType, renderLoop, sizeCanvas]);

  useEffect(() => {
    window.localStorage.setItem(visualizerEngineStorageKey, engineType);
  }, [engineType]);

  useEffect(() => {
    nativeAutomationModeRef.current = nativeAutomationMode;
    window.localStorage.setItem(nativePresetAutomationStorageKey, nativeAutomationMode);
    if (engineType === 'native' && engineRef.current?.setPresetAutomation) {
      engineRef.current.setPresetAutomation({ mode: nativeAutomationMode });
    }
  }, [engineType, nativeAutomationMode]);

  useEffect(() => {
    setNativeFavoritePresetIds((favoriteIds) => {
      const nextFavoriteIds = pruneNativePresetFavorites(favoriteIds, nativePresetLibrary);
      if (nextFavoriteIds.length !== favoriteIds.length) {
        writeStoredNativePresetFavorites(nextFavoriteIds);
        if (nextFavoriteIds.length === 0 && nativeLibraryMode === 'favorites') {
          setNativeLibraryMode('all');
          window.localStorage.setItem(nativePresetLibraryModeStorageKey, 'all');
        }
        return nextFavoriteIds;
      }
      return favoriteIds;
    });
    setNativePresetHistory((history) => {
      const libraryIds = new Set(nativePresetLibrary.map((preset) => preset.id));
      return history.filter((id) => libraryIds.has(id));
    });
  }, [nativeLibraryMode, nativePresetLibrary]);

  useEffect(() => {
    sizeCanvas();
  }, [mode, sizeCanvas]);

  useEffect(() => {
    const handleFullscreenChange = () => {
      const fsElement = document.fullscreenElement;
      if (mode === 'fullscreen' && !fsElement) {
        onModeChange('inline');
      }
    };
    document.addEventListener('fullscreenchange', handleFullscreenChange);
    return () =>
      document.removeEventListener('fullscreenchange', handleFullscreenChange);
  }, [mode, onModeChange]);

  const enterFullscreen = useCallback(async () => {
    const target = containerRef.current;
    if (!target || !target.requestFullscreen) {
      onModeChange('fullwindow');
      return;
    }
    try {
      await target.requestFullscreen();
      onModeChange('fullscreen');
    } catch {
      onModeChange('fullwindow');
    }
  }, [onModeChange]);

  const exitFullscreen = useCallback(async () => {
    if (document.fullscreenElement) {
      try {
        await document.exitFullscreen();
      } catch {
        // ignore; fullscreenchange handler will reset mode
      }
    }
    onModeChange('inline');
  }, [onModeChange]);

  const importNativePreset = useCallback(async (event) => {
    const files = Array.from(event.target.files || []);
    event.target.value = '';
    if (files.length === 0 || !engineRef.current?.loadPresetText) return;

    setError(null);
    const imported = [];
    let activePresetEntry = null;
    let importedFragmentCount = 0;
    const skipped = [];
    const { skippedTextureAssets, textureAssets } = await readNativeTextureAssets(files);

    for (const file of files.filter(isNativePresetFile)) {
      try {
        const source = await file.text();
        const presetTextureAssets = selectNativePresetTextureAssets(source, textureAssets);
        const importedPresetName = engineRef.current.inspectPresetText
          ? engineRef.current.inspectPresetText(source, file.name).title
          : engineRef.current.loadPresetText(source, file.name);
        imported.push({
          fileName: file.name,
          id: getNativePresetFileId(file),
          source,
          textureAssets: presetTextureAssets,
          title: importedPresetName,
        });
      } catch (presetError) {
        // eslint-disable-next-line no-console
        console.error('Failed to import native MilkDrop preset', presetError);
        skipped.push({
          fileName: file.name,
          message: presetError?.message || 'Unsupported syntax or shader features may be present.',
        });
      }
    }

    if (imported.length > 0) {
      const activePreset = imported[imported.length - 1];
      const activePresetName = engineRef.current.loadPresetText(
        activePreset.source,
        activePreset.fileName,
        { textureAssets: activePreset.textureAssets },
      );
      activePreset.title = activePresetName;
      activePresetEntry = activePreset;
      window.localStorage.setItem(nativePresetStorageKey, JSON.stringify(activePreset));
      setActiveNativePresetId(activePreset.id);
      setNativePresetLibrary((library) => {
        const nextLibrary = imported.reduce(
          (next, entry) => upsertNativePresetLibraryEntry(next, entry),
          library,
        );
        writeStoredNativePresetLibrary(nextLibrary);
        return nextLibrary;
      });
      setPresetName(activePresetName);
      sizeCanvas();
    }

    for (const file of files.filter(isNativeFragmentFile)) {
      if (!engineRef.current?.loadPresetFragmentText) {
        skipped.push({
          fileName: file.name,
          message: 'Native fragment import is not available.',
        });
        continue;
      }
      try {
        const source = await file.text();
        const fragmentTextureAssets = selectNativePresetTextureAssets(source, textureAssets);
        const mergedTextureAssets = {
          ...(activePresetEntry?.textureAssets || {}),
          ...fragmentTextureAssets,
        };
        const result = engineRef.current.loadPresetFragmentText(source, file.name, {
          textureAssets: mergedTextureAssets,
        });
        const existingPreset = activePresetEntry || readStoredNativePreset();
        const mergedPreset = {
          fileName: existingPreset?.fileName || file.name,
          id: existingPreset?.id || `fragment:${getNativePresetFileId(file)}`,
          source: result.source,
          textureAssets: {
            ...(existingPreset?.textureAssets || {}),
            ...fragmentTextureAssets,
          },
          title: result.title,
        };
        activePresetEntry = mergedPreset;
        importedFragmentCount += 1;
        window.localStorage.setItem(nativePresetStorageKey, JSON.stringify(mergedPreset));
        setActiveNativePresetId(mergedPreset.id);
        setNativePresetLibrary((library) => {
          const nextLibrary = upsertNativePresetLibraryEntry(library, mergedPreset);
          writeStoredNativePresetLibrary(nextLibrary);
          return nextLibrary;
        });
        setPresetName(result.title);
        sizeCanvas();
      } catch (presetError) {
        // eslint-disable-next-line no-console
        console.error('Failed to import native MilkDrop fragment', presetError);
        skipped.push({
          fileName: file.name,
          message: presetError?.message || 'Unsupported fragment syntax may be present.',
        });
      }
    }

    const importMessage = getNativePresetImportMessage({
      importedCount: imported.length + importedFragmentCount,
      skipped,
      skippedTextureAssets,
    });
    if (importMessage) {
      setError(importMessage);
    }
  }, [sizeCanvas]);

  const exportNativeFragment = useCallback((type) => {
    if (!engineRef.current?.exportPresetFragment) return;
    try {
      const exported = engineRef.current.exportPresetFragment(type);
      if (!exported) {
        setError(`No ${type} fragment is available in the active native preset.`);
        return;
      }
      downloadTextFile(exported.fileName, exported.source);
      setError(null);
    } catch (exportError) {
      // eslint-disable-next-line no-console
      console.error('Failed to export native MilkDrop fragment', exportError);
      setError(exportError?.message || 'Native fragment export failed.');
    }
  }, []);

  const loadNativeLibraryPreset = useCallback((event) => {
    const preset = nativePresetLibrary.find((entry) => entry.id === event.target.value);
    loadNativePresetEntry(preset);
  }, [loadNativePresetEntry, nativePresetLibrary]);

  const clearNativePresetLibrary = useCallback(() => {
    window.localStorage.removeItem(nativePresetStorageKey);
    window.localStorage.removeItem(nativePresetLibraryStorageKey);
    window.localStorage.removeItem(nativePresetFavoritesStorageKey);
    window.localStorage.removeItem(nativePresetLibraryModeStorageKey);
    window.localStorage.removeItem(nativePresetSearchStorageKey);
    setActiveNativePresetId('');
    setNativeFavoritePresetIds([]);
    setNativeLibraryMode('all');
    setNativePresetHistory([]);
    setNativePresetLibrary([]);
    setNativePresetSearch('');
    setError(null);
  }, []);

  const removeActiveNativePreset = useCallback(() => {
    if (!activeNativePresetId) return;
    setNativePresetLibrary((library) => {
      const nextLibrary = library.filter((preset) => preset.id !== activeNativePresetId);
      if (nextLibrary.length > 0) {
        writeStoredNativePresetLibrary(nextLibrary);
      } else {
        window.localStorage.removeItem(nativePresetLibraryStorageKey);
      }
      const nextFavoriteIds = pruneNativePresetFavorites(nativeFavoritePresetIds, nextLibrary);
      setNativeFavoritePresetIds(nextFavoriteIds);
      writeStoredNativePresetFavorites(nextFavoriteIds);
      return nextLibrary;
    });
    setNativePresetHistory((history) => history.filter((id) => id !== activeNativePresetId));
    const storedNativePreset = readStoredNativePreset();
    if (storedNativePreset?.id === activeNativePresetId) {
      window.localStorage.removeItem(nativePresetStorageKey);
    }
    setActiveNativePresetId('');
    setError(null);
  }, [activeNativePresetId, nativeFavoritePresetIds]);

  if (mode === 'off') return null;

  const className = `player-visualizer player-visualizer-${mode}`;

  return (
    <div className={className} ref={containerRef}>
      <canvas
        className="player-visualizer-canvas"
        hidden={fallbackMode}
        ref={canvasRef}
      />
      {fallbackMode ? (
        <SpectrumAnalyzer
          audioElement={audioElement}
          className="player-visualizer-fallback"
          mode="spectrum"
        />
      ) : null}
      {error ? <div className="player-visualizer-error">{error}</div> : null}
      <div className="player-visualizer-overlay">
        {mode !== 'inline' && (engineName || presetName) ? (
          <div className="player-visualizer-preset" title={presetName}>
            {[engineName, presetName].filter(Boolean).join(' · ')}
          </div>
        ) : null}
        <div
          className="player-visualizer-overlay-controls"
          onClick={(event) => event.stopPropagation()}
        >
          <input
            accept=".milk,.milk2,.shape,.wave,text/plain,image/png,image/jpeg,image/webp,image/gif"
            hidden
            multiple
            onChange={importNativePreset}
            ref={fileInputRef}
            type="file"
          />
          <input
            data-testid="visualizer-native-pack-input"
            directory=""
            hidden
            multiple
            onChange={importNativePreset}
            ref={directoryInputRef}
            type="file"
            webkitdirectory=""
          />
          <Popup
            content={`Switch visualizer engine to ${getEngineLabel(getNextEngine(engineType))}.`}
            trigger={
              <Button
                aria-label={`Switch visualizer engine to ${getEngineLabel(getNextEngine(engineType))}`}
                data-testid="visualizer-switch-engine"
                icon
                onClick={() => setEngineType((current) => getNextEngine(current))}
                size="mini"
              >
                <Icon name={engineType === 'native' ? 'microchip' : 'magic'} />
              </Button>
            }
          />
          {engineType === 'native' ? (
            <>
              {nativePresetLibrary.length > 0 ? (
                <Popup
                  content="Filter imported native presets by title or file name. The current filter also scopes next and random preset jumps."
                  trigger={
                    <input
                      aria-label="Search native MilkDrop presets"
                      className="player-visualizer-native-search"
                      data-testid="visualizer-native-preset-search"
                      onChange={updateNativePresetSearch}
                      placeholder="Search presets"
                      type="search"
                      value={nativePresetSearch}
                    />
                  }
                />
              ) : null}
              {nativePresetLibrary.length > 0 ? (
                <Popup
                  content="Clear the native preset search filter."
                  trigger={
                    <Button
                      aria-label="Clear native preset search"
                      data-testid="visualizer-clear-native-preset-search"
                      disabled={!hasNativePresetSearch}
                      icon
                      onClick={clearNativePresetSearch}
                      size="mini"
                    >
                      <Icon name="remove" />
                    </Button>
                  }
                />
              ) : null}
              {nativePresetLibrary.length > 0 ? (
                <Popup
                  content={
                    nativeLibraryMode === 'favorites'
                      ? 'Reload a favorite native preset from this browser.'
                      : 'Reload a previously imported native preset from this browser.'
                  }
                  trigger={
                    <select
                      aria-label="Native MilkDrop preset library"
                      className="player-visualizer-native-library"
                      data-testid="visualizer-native-preset-library"
                      onChange={loadNativeLibraryPreset}
                      value={selectedNativePresetValue}
                    >
                      <option value="">
                        {visibleNativePresetLibrary.length === 0 ? 'No matches' : (
                          nativeLibraryMode === 'favorites' ? 'Favorites' : 'Presets'
                        )}
                      </option>
                      {visibleNativePresetLibrary.map((preset) => (
                        <option key={preset.id} value={preset.id}>
                          {nativeFavoritePresetIds.includes(preset.id) ? '(favorite) ' : ''}
                          {preset.title || preset.fileName}
                        </option>
                      ))}
                    </select>
                  }
                />
              ) : null}
              {nativePresetLibrary.length > 0 ? (
                <Popup
                  content={
                    activeNativePresetIsFavorite
                      ? 'Remove the active native preset from favorites.'
                      : 'Mark the active native preset as a favorite.'
                  }
                  trigger={
                    <Button
                      aria-label={
                        activeNativePresetIsFavorite
                          ? 'Unfavorite active native preset'
                          : 'Favorite active native preset'
                      }
                      active={activeNativePresetIsFavorite}
                      data-testid="visualizer-toggle-native-favorite"
                      disabled={!activeNativePresetId}
                      icon
                      onClick={toggleNativePresetFavorite}
                      size="mini"
                    >
                      <Icon name={activeNativePresetIsFavorite ? 'star' : 'star outline'} />
                    </Button>
                  }
                />
              ) : null}
              {nativePresetLibrary.length > 0 ? (
                <Popup
                  content={
                    nativeLibraryMode === 'favorites'
                      ? 'Show all imported native presets.'
                      : 'Show only favorite native presets.'
                  }
                  trigger={
                    <Button
                      aria-label={
                        nativeLibraryMode === 'favorites'
                          ? 'Show all native presets'
                          : 'Show favorite native presets'
                      }
                      active={nativeLibraryMode === 'favorites'}
                      data-testid="visualizer-toggle-native-favorites-only"
                      disabled={nativeFavoritePresetIds.length === 0}
                      icon
                      onClick={toggleNativeLibraryMode}
                      size="mini"
                    >
                      <Icon name="filter" />
                    </Button>
                  }
                />
              ) : null}
              {nativePresetLibrary.length > 1 ? (
                <Popup
                  content="Return to the previous native preset, or move backward in the local preset library."
                  trigger={
                    <Button
                      aria-label="Previous native preset"
                      data-testid="visualizer-previous-native-preset"
                      disabled={visibleNativePresetLibrary.length === 0}
                      icon
                      onClick={previousNativeLibraryPreset}
                      size="mini"
                    >
                      <Icon name="step backward" />
                    </Button>
                  }
                />
              ) : null}
              {nativePresetLibrary.length > 1 ? (
                <Popup
                  content="Jump to a random imported native preset from this browser."
                  trigger={
                    <Button
                      aria-label="Random imported native preset"
                      data-testid="visualizer-random-native-preset"
                      disabled={visibleNativePresetLibrary.length === 0}
                      icon
                      onClick={randomNativeLibraryPreset}
                      size="mini"
                    >
                      <Icon name="random" />
                    </Button>
                  }
                />
              ) : null}
              {nativePresetLibrary.length > 0 ? (
                <Popup
                  content="Remove the selected native preset from this browser."
                  trigger={
                    <Button
                      aria-label="Remove selected native preset"
                      data-testid="visualizer-remove-native-preset"
                      disabled={!activeNativePresetId}
                      icon
                      onClick={removeActiveNativePreset}
                      size="mini"
                    >
                      <Icon name="minus circle" />
                    </Button>
                  }
                />
              ) : null}
              {nativePresetLibrary.length > 0 ? (
                <Popup
                  content="Clear imported native presets from this browser."
                  trigger={
                    <Button
                      aria-label="Clear imported native presets"
                      data-testid="visualizer-clear-native-preset-library"
                      icon
                      onClick={clearNativePresetLibrary}
                      size="mini"
                    >
                      <Icon name="trash alternate outline" />
                    </Button>
                  }
                />
              ) : null}
              <Popup
                content="Import a local .milk or .milk2 preset into the native WebGL renderer."
                trigger={
                  <Button
                    aria-label="Import native MilkDrop preset"
                    data-testid="visualizer-import-native-preset"
                    icon
                    onClick={() => fileInputRef.current?.click()}
                    size="mini"
                  >
                    <Icon name="upload" />
                  </Button>
                }
              />
              <Popup
                content="Import a native MilkDrop preset folder with its local image assets."
                trigger={
                  <Button
                    aria-label="Import native MilkDrop preset folder"
                    data-testid="visualizer-import-native-preset-folder"
                    icon
                    onClick={() => directoryInputRef.current?.click()}
                    size="mini"
                  >
                    <Icon name="folder open outline" />
                  </Button>
                }
              />
              <Popup
                content="Export the first custom shape in the active native preset as a .shape fragment."
                trigger={
                  <Button
                    aria-label="Export native MilkDrop shape fragment"
                    data-testid="visualizer-export-native-shape"
                    icon
                    onClick={() => exportNativeFragment('shape')}
                    size="mini"
                  >
                    <Icon name="download" />
                  </Button>
                }
              />
              <Popup
                content="Export the first custom wave in the active native preset as a .wave fragment."
                trigger={
                  <Button
                    aria-label="Export native MilkDrop wave fragment"
                    data-testid="visualizer-export-native-wave"
                    icon
                    onClick={() => exportNativeFragment('wave')}
                    size="mini"
                  >
                    <Icon name="download" />
                  </Button>
                }
              />
              <Popup
                content={`Native automatic preset changes: ${getNativeAutomationLabel(nativeAutomationMode)}. Beat mode advances after repeated detected bass beats; timed mode advances on an interval.`}
                trigger={
                  <Button
                    aria-label={`Native automatic preset changes: ${getNativeAutomationLabel(nativeAutomationMode)}`}
                    active={nativeAutomationMode !== 'off'}
                    data-testid="visualizer-native-automation"
                    icon
                    onClick={cycleNativeAutomationMode}
                    size="mini"
                  >
                    <Icon name={nativeAutomationMode === 'beat' ? 'heartbeat' : 'clock outline'} />
                  </Button>
                }
              />
            </>
          ) : null}
          <Popup
            content={
              nativeBankNavigationDisabled
                ? 'No imported native presets match the current filter.'
                : 'Load a different MilkDrop preset.'
            }
            trigger={
              <Button
                aria-label="Next visualizer preset"
                data-testid="visualizer-next-preset"
                disabled={nativeBankNavigationDisabled}
                icon
                onClick={cyclePreset}
                size="mini"
              >
                <Icon name="random" />
              </Button>
            }
          />
          {mode === 'inline' ? (
            <>
              <Popup
                content="Expand visualizer to fill the browser window."
                trigger={
                  <Button
                    aria-label="Expand visualizer to full browser window"
                    data-testid="visualizer-fullwindow"
                    icon
                    onClick={() => onModeChange('fullwindow')}
                    size="mini"
                  >
                    <Icon name="expand arrows alternate" />
                  </Button>
                }
              />
              <Popup
                content="Enter true fullscreen."
                trigger={
                  <Button
                    aria-label="Enter fullscreen visualizer"
                    data-testid="visualizer-fullscreen"
                    icon
                    onClick={enterFullscreen}
                    size="mini"
                  >
                    <Icon name="expand" />
                  </Button>
                }
              />
            </>
          ) : (
            <>
              {mode === 'fullwindow' ? (
                <Popup
                  content="Enter true fullscreen."
                  trigger={
                    <Button
                      aria-label="Enter fullscreen visualizer"
                      data-testid="visualizer-fullscreen"
                      icon
                      onClick={enterFullscreen}
                      size="mini"
                    >
                      <Icon name="expand" />
                    </Button>
                  }
                />
              ) : null}
              <Popup
                content="Return visualizer to the player bar."
                trigger={
                  <Button
                    aria-label="Collapse visualizer"
                    data-testid="visualizer-collapse"
                    icon
                    onClick={exitFullscreen}
                    size="mini"
                  >
                    <Icon name="compress" />
                  </Button>
                }
              />
            </>
          )}
        </div>
      </div>
    </div>
  );
};

export default Visualizer;
