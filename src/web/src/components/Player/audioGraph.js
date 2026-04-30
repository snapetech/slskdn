const audioGraphCache = new WeakMap();

const eqBands = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

const disconnect = (node) => {
  try {
    node.disconnect();
  } catch {
    // Nodes may already be disconnected when the graph is rebuilt.
  }
};

const rebuildGraph = (graph) => {
  const {
    analyser,
    ctx,
    eq,
    inputGain,
    outputGain,
    source,
    visualizerInput,
  } = graph;

  disconnect(source);
  disconnect(inputGain);
  graph.karaokeNodes.forEach(disconnect);
  eq.forEach(disconnect);
  disconnect(analyser);
  disconnect(outputGain);

  source.connect(inputGain);
  let tail = inputGain;

  if (graph.karaokeEnabled) {
    const splitter = ctx.createChannelSplitter(2);
    const leftGain = ctx.createGain();
    const rightGain = ctx.createGain();
    const merger = ctx.createChannelMerger(2);

    rightGain.gain.value = -1;
    tail.connect(splitter);
    splitter.connect(leftGain, 0);
    splitter.connect(rightGain, 1);
    leftGain.connect(merger, 0, 0);
    rightGain.connect(merger, 0, 1);

    graph.karaokeNodes = [splitter, leftGain, rightGain, merger];
    tail = merger;
  } else {
    graph.karaokeNodes = [];
  }

  eq.forEach((filter) => {
    tail.connect(filter);
    tail = filter;
  });

  tail.connect(visualizerInput);
  tail.connect(analyser);
  analyser.connect(outputGain);
  outputGain.connect(ctx.destination);
};

export const getOrCreateAudioGraph = (audioElement) => {
  if (!audioElement || typeof window === 'undefined') return null;
  const cached = audioGraphCache.get(audioElement);
  if (cached) return cached;

  const AudioCtx = window.AudioContext || window.webkitAudioContext;
  if (!AudioCtx) return null;

  const ctx = new AudioCtx();
  const source = ctx.createMediaElementSource(audioElement);
  const inputGain = ctx.createGain();
  const outputGain = ctx.createGain();
  const visualizerInput = ctx.createGain();
  const visualizerOutput = ctx.createGain();
  const analyser = ctx.createAnalyser();
  const eq = eqBands.map((frequency) => {
    const filter = ctx.createBiquadFilter();
    filter.type = 'peaking';
    filter.frequency.value = frequency;
    filter.Q.value = 1.15;
    filter.gain.value = 0;
    return filter;
  });

  analyser.fftSize = 2048;
  outputGain.gain.value = 1;
  visualizerOutput.gain.value = 0;
  visualizerInput.connect(visualizerOutput);
  visualizerOutput.connect(ctx.destination);

  const graph = {
    analyser,
    ctx,
    eq,
    inputGain,
    karaokeEnabled: false,
    karaokeNodes: [],
    outputGain,
    source,
    visualizerInput,
    visualizerOutput,
  };

  rebuildGraph(graph);
  audioGraphCache.set(audioElement, graph);
  return graph;
};

export const resumeAudioGraph = async (audioElement) => {
  const graph = getOrCreateAudioGraph(audioElement);
  if (graph?.ctx.state === 'suspended') {
    await graph.ctx.resume();
  }
  return graph;
};

export const setEqGains = (audioElement, gains) => {
  const graph = getOrCreateAudioGraph(audioElement);
  if (!graph) return;
  graph.eq.forEach((filter, index) => {
    filter.gain.value = gains[index] || 0;
  });
};

export const setKaraokeEnabled = (audioElement, enabled) => {
  const graph = getOrCreateAudioGraph(audioElement);
  if (!graph || graph.karaokeEnabled === enabled) return;
  graph.karaokeEnabled = enabled;
  rebuildGraph(graph);
};

export const setOutputGain = (audioElement, value) => {
  const graph = getOrCreateAudioGraph(audioElement);
  if (!graph) {
    audioElement.volume = Math.max(0, Math.min(1, value));
    return;
  }
  graph.outputGain.gain.cancelScheduledValues(graph.ctx.currentTime);
  graph.outputGain.gain.setValueAtTime(value, graph.ctx.currentTime);
};

export const fadeOutputGain = (audioElement, from, to, durationSeconds) => {
  const graph = getOrCreateAudioGraph(audioElement);
  if (!graph) {
    audioElement.volume = Math.max(0, Math.min(1, to));
    return;
  }

  const now = graph.ctx.currentTime;
  graph.outputGain.gain.cancelScheduledValues(now);
  graph.outputGain.gain.setValueAtTime(from, now);
  graph.outputGain.gain.linearRampToValueAtTime(to, now + durationSeconds);
};

export const bands = eqBands;
