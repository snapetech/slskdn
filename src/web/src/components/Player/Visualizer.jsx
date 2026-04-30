import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Button, Icon, Popup } from 'semantic-ui-react';
import { resumeAudioGraph } from './audioGraph';
import SpectrumAnalyzer from './SpectrumAnalyzer';
import { createButterchurnEngine } from './visualizers/butterchurnEngine';
import { createNativeMilkdropEngine } from './visualizers/nativeMilkdropEngine';

const visualizerEngineStorageKey = 'slskdn.player.visualizerEngine';
const nativePresetStorageKey = 'slskdn.player.nativeMilkdropPreset';
const nativePresetLibraryStorageKey = 'slskdn.player.nativeMilkdropPresetLibrary';
const nativePresetLibraryLimit = 20;

const readStoredEngine = () => {
  if (typeof window === 'undefined') return 'butterchurn';
  return window.localStorage.getItem(visualizerEngineStorageKey) === 'native'
    ? 'native'
    : 'butterchurn';
};

const getNextEngine = (engine) => (engine === 'native' ? 'butterchurn' : 'native');

const getEngineLabel = (engine) => (engine === 'native' ? 'slskdN native' : 'Butterchurn');

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

const writeStoredNativePresetLibrary = (library) => {
  window.localStorage.setItem(
    nativePresetLibraryStorageKey,
    JSON.stringify(library.slice(0, nativePresetLibraryLimit)),
  );
};

const upsertNativePresetLibraryEntry = (library, entry) => [
  entry,
  ...library.filter((preset) => preset.id !== entry.id),
].slice(0, nativePresetLibraryLimit);

const getNativePresetFileId = (file) =>
  [file.name, file.size, file.lastModified].filter((part) => part !== undefined).join(':');

const getNativePresetImportMessage = ({ importedCount, skipped }) => {
  if (skipped.length === 0) return null;
  const skippedNames = skipped.slice(0, 3).map((preset) => preset.fileName).join(', ');
  const remaining = skipped.length > 3 ? `, +${skipped.length - 3} more` : '';
  const prefix = importedCount > 0
    ? `Imported ${importedCount}; skipped ${skipped.length}`
    : `Native preset import failed for ${skipped.length}`;
  return `${prefix}: ${skippedNames}${remaining}.`;
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
  const engineRef = useRef(null);
  const fileInputRef = useRef(null);
  const rafRef = useRef(null);
  const engineAudioNodeRef = useRef(null);
  const [fallbackMode, setFallbackMode] = useState(false);
  const [engineType, setEngineType] = useState(readStoredEngine);
  const [engineName, setEngineName] = useState('');
  const [activeNativePresetId, setActiveNativePresetId] = useState(
    () => readStoredNativePreset()?.id || '',
  );
  const [nativePresetLibrary, setNativePresetLibrary] = useState(readStoredNativePresetLibrary);
  const [presetName, setPresetName] = useState('');
  const [error, setError] = useState(null);

  const renderLoop = useCallback(() => {
    if (!engineRef.current) return;
    try {
      engineRef.current.render();
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

  const cyclePreset = useCallback(() => {
    if (!engineRef.current) return;
    const nextPresetName = engineRef.current.nextPreset();
    if (nextPresetName) {
      setPresetName(nextPresetName);
    }
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
        const storedNativePreset = engineType === 'native' ? readStoredNativePreset() : null;
        if (storedNativePreset?.source && engine.loadPresetText) {
          const importedPresetName = engine.loadPresetText(
            storedNativePreset.source,
            storedNativePreset.fileName,
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
    const skipped = [];

    for (const file of files) {
      try {
        const source = await file.text();
        const importedPresetName = engineRef.current.inspectPresetText
          ? engineRef.current.inspectPresetText(source, file.name).title
          : engineRef.current.loadPresetText(source, file.name);
        imported.push({
          fileName: file.name,
          id: getNativePresetFileId(file),
          source,
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
      );
      activePreset.title = activePresetName;
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

    const importMessage = getNativePresetImportMessage({
      importedCount: imported.length,
      skipped,
    });
    if (importMessage) {
      setError(importMessage);
    }
  }, [sizeCanvas]);

  const loadNativeLibraryPreset = useCallback((event) => {
    const preset = nativePresetLibrary.find((entry) => entry.id === event.target.value);
    if (!preset || !engineRef.current?.loadPresetText) return;

    try {
      setError(null);
      const loadedPresetName = engineRef.current.loadPresetText(preset.source, preset.fileName);
      window.localStorage.setItem(nativePresetStorageKey, JSON.stringify(preset));
      setActiveNativePresetId(preset.id);
      setPresetName(loadedPresetName);
      sizeCanvas();
    } catch (presetError) {
      // eslint-disable-next-line no-console
      console.error('Failed to load native MilkDrop preset from library', presetError);
      setError(presetError?.message || 'Native preset load failed.');
    }
  }, [nativePresetLibrary, sizeCanvas]);

  const clearNativePresetLibrary = useCallback(() => {
    window.localStorage.removeItem(nativePresetStorageKey);
    window.localStorage.removeItem(nativePresetLibraryStorageKey);
    setActiveNativePresetId('');
    setNativePresetLibrary([]);
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
      return nextLibrary;
    });
    const storedNativePreset = readStoredNativePreset();
    if (storedNativePreset?.id === activeNativePresetId) {
      window.localStorage.removeItem(nativePresetStorageKey);
    }
    setActiveNativePresetId('');
    setError(null);
  }, [activeNativePresetId]);

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
            accept=".milk,.milk2,text/plain"
            hidden
            multiple
            onChange={importNativePreset}
            ref={fileInputRef}
            type="file"
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
                  content="Reload a previously imported native preset."
                  trigger={
                    <select
                      aria-label="Native MilkDrop preset library"
                      className="player-visualizer-native-library"
                      data-testid="visualizer-native-preset-library"
                      onChange={loadNativeLibraryPreset}
                      value={activeNativePresetId}
                    >
                      <option value="">Presets</option>
                      {nativePresetLibrary.map((preset) => (
                        <option key={preset.id} value={preset.id}>
                          {preset.title || preset.fileName}
                        </option>
                      ))}
                    </select>
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
            </>
          ) : null}
          <Popup
            content="Load a different MilkDrop preset."
            trigger={
              <Button
                aria-label="Next visualizer preset"
                data-testid="visualizer-next-preset"
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
