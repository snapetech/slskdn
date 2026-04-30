import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Button, Icon, Popup } from 'semantic-ui-react';

const audioGraphCache = new WeakMap();

const getOrCreateAudioGraph = (audioElement) => {
  if (!audioElement) return null;
  const cached = audioGraphCache.get(audioElement);
  if (cached) return cached;

  const AudioCtx = window.AudioContext || window.webkitAudioContext;
  if (!AudioCtx) return null;

  const ctx = new AudioCtx();
  const source = ctx.createMediaElementSource(audioElement);
  source.connect(ctx.destination);
  const graph = { ctx, source };
  audioGraphCache.set(audioElement, graph);
  return graph;
};

const pickRandomPreset = (presets) => {
  const names = Object.keys(presets);
  if (names.length === 0) return null;
  const name = names[Math.floor(Math.random() * names.length)];
  return { data: presets[name], name };
};

const Visualizer = ({ audioElement, mode, onModeChange }) => {
  const containerRef = useRef(null);
  const canvasRef = useRef(null);
  const visualizerRef = useRef(null);
  const rafRef = useRef(null);
  const presetsRef = useRef(null);
  const [presetName, setPresetName] = useState('');
  const [error, setError] = useState(null);

  const renderLoop = useCallback(() => {
    if (!visualizerRef.current) return;
    visualizerRef.current.render();
    rafRef.current = window.requestAnimationFrame(renderLoop);
  }, []);

  const cyclePreset = useCallback(() => {
    if (!visualizerRef.current || !presetsRef.current) return;
    const picked = pickRandomPreset(presetsRef.current);
    if (!picked) return;
    visualizerRef.current.loadPreset(picked.data, 2.0);
    setPresetName(picked.name);
  }, []);

  const sizeCanvas = useCallback(() => {
    const container = containerRef.current;
    const canvas = canvasRef.current;
    const visualizer = visualizerRef.current;
    if (!container || !canvas || !visualizer) return;
    const rect = container.getBoundingClientRect();
    const width = Math.max(1, Math.floor(rect.width));
    const height = Math.max(1, Math.floor(rect.height));
    canvas.width = width;
    canvas.height = height;
    visualizer.setRendererSize(width, height);
  }, []);

  useEffect(() => {
    if (mode === 'off' || !audioElement || !canvasRef.current) return undefined;

    let cancelled = false;
    let resizeObserver = null;

    (async () => {
      try {
        const graph = getOrCreateAudioGraph(audioElement);
        if (!graph) {
          setError('Web Audio is not available in this browser.');
          return;
        }
        if (graph.ctx.state === 'suspended') {
          await graph.ctx.resume();
        }

        const [butterchurnModule, presetsModule] = await Promise.all([
          import('butterchurn'),
          import('butterchurn-presets'),
        ]);
        if (cancelled) return;

        const butterchurn = butterchurnModule.default || butterchurnModule;
        const presetsApi = presetsModule.default || presetsModule;
        const presets = presetsApi.getPresets();
        presetsRef.current = presets;

        const visualizer = butterchurn.createVisualizer(
          graph.ctx,
          canvasRef.current,
          {
            height: 600,
            pixelRatio: window.devicePixelRatio || 1,
            textureRatio: 1,
            width: 800,
          },
        );
        visualizer.connectAudio(graph.source);

        const picked = pickRandomPreset(presets);
        if (picked) {
          visualizer.loadPreset(picked.data, 0);
          setPresetName(picked.name);
        }

        visualizerRef.current = visualizer;
        sizeCanvas();

        if (typeof window.ResizeObserver === 'function' && containerRef.current) {
          resizeObserver = new window.ResizeObserver(() => sizeCanvas());
          resizeObserver.observe(containerRef.current);
        }

        rafRef.current = window.requestAnimationFrame(renderLoop);
      } catch (importError) {
        // eslint-disable-next-line no-console
        console.error('Failed to load Milkdrop visualizer', importError);
        setError('Failed to load visualizer.');
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
      visualizerRef.current = null;
      presetsRef.current = null;
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
        // ignore — fullscreenchange handler will reset mode
      }
    }
    onModeChange('inline');
  }, [onModeChange]);

  if (mode === 'off') return null;

  const className = `player-visualizer player-visualizer-${mode}`;

  return (
    <div className={className} ref={containerRef}>
      <canvas className="player-visualizer-canvas" ref={canvasRef} />
      {error ? <div className="player-visualizer-error">{error}</div> : null}
      <div className="player-visualizer-overlay">
        {mode !== 'inline' && presetName ? (
          <div className="player-visualizer-preset" title={presetName}>
            {presetName}
          </div>
        ) : null}
        <div className="player-visualizer-overlay-controls">
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
