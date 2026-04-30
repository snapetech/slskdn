import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Button, Icon, Popup } from 'semantic-ui-react';
import { resumeAudioGraph } from './audioGraph';
import SpectrumAnalyzer from './SpectrumAnalyzer';
import { createButterchurnEngine } from './visualizers/butterchurnEngine';

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
  const rafRef = useRef(null);
  const engineAudioNodeRef = useRef(null);
  const [fallbackMode, setFallbackMode] = useState(false);
  const [engineName, setEngineName] = useState('');
  const [presetName, setPresetName] = useState('');
  const [error, setError] = useState(null);

  const renderLoop = useCallback(() => {
    if (!engineRef.current) return;
    engineRef.current.render();
    rafRef.current = window.requestAnimationFrame(renderLoop);
  }, []);

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

        const engine = await createButterchurnEngine({
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
        sizeCanvas();

        if (typeof window.ResizeObserver === 'function' && containerRef.current) {
          resizeObserver = new window.ResizeObserver(() => sizeCanvas());
          resizeObserver.observe(containerRef.current);
        }

        rafRef.current = window.requestAnimationFrame(renderLoop);
      } catch (importError) {
        // eslint-disable-next-line no-console
        console.error('Failed to load Milkdrop visualizer', importError);
        setError('MilkDrop failed. Showing analyzer fallback.');
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
  }, [mode, audioElement, renderLoop, sizeCanvas]);

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
