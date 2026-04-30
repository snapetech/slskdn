const pickRandomPreset = (presets) => {
  const names = Object.keys(presets);
  if (names.length === 0) return null;
  const name = names[Math.floor(Math.random() * names.length)];
  return { data: presets[name], name };
};

const resolveButterchurnApi = (butterchurnModule) => {
  const candidates = [
    butterchurnModule,
    butterchurnModule.default,
    butterchurnModule.default?.default,
  ];
  return candidates.find((candidate) => candidate?.createVisualizer);
};

export const createButterchurnEngine = async ({
  audioContext,
  audioNode,
  canvas,
  pixelRatio,
}) => {
  const [butterchurnModule, presetsModule] = await Promise.all([
    import('butterchurn'),
    import('butterchurn-presets'),
  ]);

  const butterchurn = resolveButterchurnApi(butterchurnModule);
  if (!butterchurn) {
    throw new Error('Butterchurn visualizer API was not found.');
  }

  const presetsApi = presetsModule.default || presetsModule;
  const presets = presetsApi.getPresets();
  const visualizer = butterchurn.createVisualizer(
    audioContext,
    canvas,
    {
      height: 600,
      pixelRatio,
      textureRatio: 1,
      width: 800,
    },
  );
  visualizer.connectAudio(audioNode);

  const loadRandomPreset = (blendSeconds) => {
    const picked = pickRandomPreset(presets);
    if (!picked) return '';
    visualizer.loadPreset(picked.data, blendSeconds);
    return picked.name;
  };

  const presetName = loadRandomPreset(0);

  return {
    name: 'Butterchurn',
    presetName,
    dispose: () => {
      visualizer.disconnectAudio(audioNode);
    },
    nextPreset: () => loadRandomPreset(2.0),
    render: () => {
      visualizer.render();
    },
    resize: (width, height) => {
      visualizer.setRendererSize(width, height);
    },
  };
};
