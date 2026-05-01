import { createMilkdropRenderer } from './milkdrop/milkdropRenderer';
import {
  analyzeMilkdropPresetCompatibility,
  getMilkdropCompatibilityError,
} from './milkdrop/presetCompatibility';
import {
  parseMilkdropFragment,
  parseMilkdropPreset,
  serializeMilkdropFragment,
  serializeMilkdropPresetSet,
} from './milkdrop/presetParser';

const nativePresets = [
  {
    name: 'slskdN native grid smoke',
    source: `
      name=slskdN native grid smoke
      decay=0.91
      wave_r=0.12
      wave_g=0.64
      wave_b=0.88
      wave_scale=1.2
      zoom=1
      rot=0
      per_frame_1=wave_r=0.35+0.25*bass_att;
      per_frame_2=wave_g=0.45+0.2*mid_att;
      per_frame_3=wave_b=0.55+0.2*treb_att;
      per_frame_4=rot=0.01*sin(time*0.7);
      per_frame_5=zoom=1+0.03*sin(time*0.5);
      per_pixel_1=dx=0.015*sin((x+time)*6.283);
      per_pixel_2=dy=0.015*cos((y+time)*6.283);
      mv_x=8
      mv_y=5
      mv_dx=0.2
      mv_dy=0.1
      mv_l=0.15
      mv_a=0.32
      shape00_enabled=1
      shape00_sides=5
      shape00_rad=0.18
      shape00_x=0.5
      shape00_y=0.5
      shape00_r=0.1
      shape00_g=0.9
      shape00_b=0.45
      shape00_a=0.35
      shape00_r2=0.9
      shape00_g2=0.8
      shape00_b2=0.2
      shape00_a2=0.18
      shape00_border_a=0.9
      shape00_per_frame1=ang=time*0.5;
      wavecode_0_enabled=1
      wavecode_0_samples=96
      wavecode_0_spectrum=1
      wavecode_0_dots=1
      wavecode_0_r=0.7
      wavecode_0_g=0.95
      wavecode_0_b=0.25
      wavecode_0_a=0.75
      wavecode_0_per_point1=x=i;
      wavecode_0_per_point2=y=0.08+sample*0.55;
    `,
  },
  {
    name: 'slskdN native waveform smoke',
    source: `
      name=slskdN native waveform smoke
      decay=0.88
      wave_r=0.85
      wave_g=0.34
      wave_b=0.18
      wave_scale=1.5
      per_frame_1=dx=0.02*sin(time*0.4);
      per_frame_2=dy=0.015*cos(time*0.3);
      per_frame_3=rot=0.02*sin(time*0.2);
      shape00_enabled=1
      shape00_sides=3
      shape00_rad=0.12+0.03*bass_att
      shape00_x=0.35
      shape00_y=0.55
      shape00_r=0.9
      shape00_g=0.2
      shape00_b=0.1
      shape00_a=0.28
      shape00_additive=1
      shape01_enabled=1
      shape01_sides=6
      shape01_rad=0.08+0.02*treb_att
      shape01_x=0.67
      shape01_y=0.45
      shape01_r=0.1
      shape01_g=0.55
      shape01_b=0.95
      shape01_a=0.35
      wavecode_0_enabled=1
      wavecode_0_samples=128
      wavecode_0_r=0.95
      wavecode_0_g=0.85
      wavecode_0_b=0.2
      wavecode_0_a=0.8
      wavecode_0_per_point1=x=i;
      wavecode_0_per_point2=y=0.5+sample*0.35;
    `,
  },
];

const createFrameReader = (audioContext, audioNode) => {
  const analyser = audioContext.createAnalyser();
  analyser.fftSize = 2048;
  audioNode.connect(analyser);

  const waveform = new Uint8Array(analyser.fftSize);
  const frequency = new Uint8Array(analyser.frequencyBinCount);

  return {
    disconnect: () => {
      try {
        audioNode.disconnect(analyser);
      } catch {
        // The shared audio graph may have been rebuilt or torn down first.
      }
    },
    read: () => {
      analyser.getByteTimeDomainData(waveform);
      analyser.getByteFrequencyData(frequency);
      return {
        samples: Array.from(waveform, (value) => (value - 128) / 128),
        spectrum: frequency,
      };
    },
  };
};

const getParsedPresetSetTitle = (parsed, fileName) => {
  const titles = parsed.presets
    .map((preset) => preset.metadata?.title)
    .filter(Boolean);
  if (titles.length > 1) return titles.join(' + ');
  return titles[0] || fileName || 'Imported preset';
};

const getCompatibilityErrors = (parsed) =>
  parsed.presets
    .map((preset, index) => ({
      index,
      message: getMilkdropCompatibilityError(analyzeMilkdropPresetCompatibility(preset)),
    }))
    .filter((entry) => entry.message);

const formatCompatibilityError = (parsed, errors) => {
  if (parsed.presets.length === 1 && errors.length === 1) return errors[0].message;
  return errors
    .map((entry) => `preset ${entry.index + 1}: ${entry.message}`)
    .join('; ');
};

const parseCompatiblePresetText = (source, fileName = '') => {
  const parsed = parseMilkdropPreset(source, {
    format: fileName.toLowerCase().endsWith('.milk2') ? 'milk2' : undefined,
  });
  const compatibilityErrors = getCompatibilityErrors(parsed);
  if (compatibilityErrors.length > 0) {
    throw new Error(formatCompatibilityError(parsed, compatibilityErrors));
  }
  return parsed;
};

const cloneEntry = (entry = {}) => ({
  baseValues: { ...(entry.baseValues || {}) },
  equations: { ...(entry.equations || {}) },
});

const clonePreset = (preset) => ({
  baseValues: { ...(preset.baseValues || {}) },
  equations: { ...(preset.equations || {}) },
  index: preset.index,
  metadata: { ...(preset.metadata || {}) },
  rawSections: { ...(preset.rawSections || {}) },
  shaders: { ...(preset.shaders || {}) },
  shapes: (preset.shapes || []).map(cloneEntry),
  sprites: (preset.sprites || []).map(cloneEntry),
  source: preset.source,
  waves: (preset.waves || []).map(cloneEntry),
});

const cloneParsedPresetSet = (parsed) => {
  const presets = parsed.presets.map(clonePreset);
  return {
    format: parsed.format,
    presets,
    primary: presets[0],
  };
};

const mergeFragmentIntoPresetSet = (parsed, fragment) => {
  const merged = cloneParsedPresetSet(parsed);
  const target = merged.primary;
  const targetEntries = fragment.type === 'wave' ? target.waves : target.shapes;
  fragment.entries.forEach((entry) => {
    targetEntries.push(cloneEntry(entry));
  });
  target.source = serializeMilkdropPresetSet(merged);
  return merged;
};

const removeFragmentFromPresetSet = (parsed, type, index) => {
  const removed = cloneParsedPresetSet(parsed);
  const targetEntries = type === 'wave'
    ? removed.primary.waves
    : removed.primary.shapes;
  if (!targetEntries[index]) return null;
  targetEntries.splice(index, 1);
  removed.primary.source = serializeMilkdropPresetSet(removed);
  return removed;
};

const updatePresetBaseValue = (parsed, key, value) => {
  const updated = cloneParsedPresetSet(parsed);
  updated.primary.baseValues[key] = value;
  updated.primary.source = serializeMilkdropPresetSet(updated);
  return updated;
};

const getFragmentEntryLabel = (entry, index, type) => {
  const prefix = type === 'wave' ? 'Wave' : 'Shape';
  const values = entry?.baseValues || {};
  const labelParts = [
    values.name,
    values.label,
    values.tex_name,
    values.texname,
    values.texture,
    values.image,
  ].filter(Boolean);
  if (labelParts.length > 0) {
    return `${prefix} ${index + 1}: ${labelParts[0]}`;
  }
  if (type === 'wave') {
    const samples = values.samples ?? values.nsamples;
    return samples ? `${prefix} ${index + 1}: ${samples} samples` : `${prefix} ${index + 1}`;
  }
  const sides = values.sides ?? values.numsides;
  return sides ? `${prefix} ${index + 1}: ${sides} sides` : `${prefix} ${index + 1}`;
};

const getPresetFragmentSummary = (parsed) => ({
  shapes: (parsed.primary?.shapes || []).map((entry, index) => ({
    index,
    label: getFragmentEntryLabel(entry, index, 'shape'),
  })),
  waves: (parsed.primary?.waves || []).map((entry, index) => ({
    index,
    label: getFragmentEntryLabel(entry, index, 'wave'),
  })),
});

const editableParameterSpecs = {
  decay: { defaultValue: 0.9, max: 1, min: 0.5 },
  rot: { defaultValue: 0, max: 0.5, min: -0.5 },
  wave_a: { defaultValue: 1, max: 1, min: 0 },
  wave_b: { defaultValue: 0.7, max: 1, min: 0 },
  wave_g: { defaultValue: 0.7, max: 1, min: 0 },
  wave_r: { defaultValue: 0.7, max: 1, min: 0 },
  zoom: { defaultValue: 1, max: 1.5, min: 0.5 },
};
const editableParameterKeys = Object.keys(editableParameterSpecs);

const getPresetParameterSummary = (parsed) => {
  const values = parsed.primary?.baseValues || {};
  return editableParameterKeys.reduce((summary, key) => ({
    ...summary,
    [key]: Number.isFinite(Number(values[key])) ? Number(values[key]) : undefined,
  }), {});
};

const getRandomizedPresetParameters = (parsed, random = Math.random) => {
  const values = parsed.primary?.baseValues || {};
  return editableParameterKeys.reduce((nextValues, key) => {
    const spec = editableParameterSpecs[key];
    const currentValue = Number.isFinite(Number(values[key]))
      ? Number(values[key])
      : spec.defaultValue;
    const jitteredValue = spec.min + (random() * (spec.max - spec.min));
    return {
      ...nextValues,
      [key]: Number(((currentValue + jitteredValue) / 2).toFixed(2)),
    };
  }, {});
};

const updatePresetBaseValues = (parsed, values) => {
  const updated = cloneParsedPresetSet(parsed);
  Object.entries(values).forEach(([key, value]) => {
    updated.primary.baseValues[key] = value;
  });
  updated.primary.source = serializeMilkdropPresetSet(updated);
  return updated;
};

const getPresetDebugSnapshot = (parsed, title) => ({
  format: parsed.format,
  parameters: getPresetParameterSummary(parsed),
  presetCount: parsed.presets.length,
  shaderSections: {
    comp: Boolean(parsed.primary?.shaders?.comp),
    warp: Boolean(parsed.primary?.shaders?.warp),
  },
  shapes: parsed.primary?.shapes?.length || 0,
  sprites: parsed.primary?.sprites?.length || 0,
  title,
  waves: parsed.primary?.waves?.length || 0,
});

const defaultTransitionSeconds = 1.5;
const defaultTransitionMode = 'crossfade';
const defaultAutomation = {
  beatSensitivity: 1.35,
  beatsPerPreset: 8,
  minBeatIntervalSeconds: 0.25,
  mode: 'off',
  timedIntervalSeconds: 30,
};

const normalizeAutomation = (automation = {}) => ({
  ...defaultAutomation,
  ...automation,
  mode: ['beat', 'timed'].includes(automation.mode) ? automation.mode : 'off',
});

export const getNativeMilkdropTransitionProgress = (startedAt, seconds, now) => {
  if (!Number.isFinite(seconds) || seconds <= 0) return 1;
  const linear = Math.max(0, Math.min(1, (now - startedAt) / seconds));
  return linear * linear * (3 - linear * 2);
};

const normalizeTransitionMode = (value) => {
  const mode = String(value || '').trim().toLowerCase().replace(/[\s_-]+/g, '');
  if (['cut', 'instant', 'none'].includes(mode)) return 'cut';
  if (['fade', 'fadeblack', 'fadethroughblack'].includes(mode)) return 'fade';
  if (['overlay', 'burnin', 'hold'].includes(mode)) return 'overlay';
  return defaultTransitionMode;
};

export const getNativeMilkdropTransitionAlphas = (progress, mode = defaultTransitionMode) => {
  const clampedProgress = Math.max(0, Math.min(1, Number(progress) || 0));
  const normalizedMode = normalizeTransitionMode(mode);
  if (normalizedMode === 'fade') {
    return {
      incoming: clampedProgress <= 0.5 ? 0 : (clampedProgress - 0.5) * 2,
      outgoing: clampedProgress >= 0.5 ? 0 : 1 - (clampedProgress * 2),
    };
  }
  if (normalizedMode === 'overlay') {
    return {
      incoming: clampedProgress,
      outgoing: 1,
    };
  }
  if (normalizedMode === 'cut') {
    return {
      incoming: 1,
      outgoing: 0,
    };
  }
  return {
    incoming: clampedProgress,
    outgoing: 1 - clampedProgress,
  };
};

const getSpectrumEnergy = (spectrum = []) => {
  if (!spectrum.length) return 0;
  const limit = Math.max(1, Math.min(24, spectrum.length));
  let total = 0;
  for (let index = 0; index < limit; index += 1) {
    total += Number(spectrum[index]) || 0;
  }
  return total / (limit * 255);
};

export const getNativeMilkdropBeatUpdate = (
  previous = {},
  spectrum = [],
  now = 0,
  automation = defaultAutomation,
) => {
  const energy = getSpectrumEnergy(spectrum);
  const smoothedEnergy = previous.smoothedEnergy === undefined
    ? energy
    : (previous.smoothedEnergy * 0.85) + (energy * 0.15);
  const secondsSinceBeat = now - (previous.lastBeatAt ?? -Infinity);
  const isBeat = energy > Math.max(0.05, smoothedEnergy * automation.beatSensitivity)
    && secondsSinceBeat >= automation.minBeatIntervalSeconds;
  const beatCount = isBeat ? (previous.beatCount || 0) + 1 : (previous.beatCount || 0);
  return {
    beatCount,
    energy,
    isBeat,
    lastBeatAt: isBeat ? now : previous.lastBeatAt,
    smoothedEnergy,
  };
};

const disposeRendererSet = (rendererSet) => {
  rendererSet.entries.forEach((entry) => entry.renderer.dispose());
};

const disposeRendererSets = (rendererSets) => {
  rendererSets.forEach(disposeRendererSet);
};

const getCompositeAlpha = (preset, index) => {
  if (index === 0) return 1;
  const configuredAlpha = Number(
    preset.baseValues?.blend_alpha
    ?? preset.baseValues?.blendalpha
    ?? preset.baseValues?.composite_alpha
    ?? preset.baseValues?.alpha,
  );
  return Number.isFinite(configuredAlpha)
    ? Math.max(0, Math.min(1, configuredAlpha))
    : 0.5;
};

const normalizeCompositeMode = (value) => {
  const mode = String(value || '').trim().toLowerCase().replace(/[\s_-]+/g, '');
  if (['add', 'additive', 'plus'].includes(mode)) return 'additive';
  if (['screen'].includes(mode)) return 'screen';
  if (['multiply', 'mult'].includes(mode)) return 'multiply';
  return 'alpha';
};

const getCompositeMode = (preset, index) => {
  if (index === 0) return 'alpha';
  return normalizeCompositeMode(
    preset.baseValues?.blend_mode
    ?? preset.baseValues?.blendmode
    ?? preset.baseValues?.composite_mode
    ?? preset.baseValues?.compositemode
    ?? preset.baseValues?.mode,
  );
};

const getPresetSetTransitionSeconds = (parsed, fallback = defaultTransitionSeconds) => {
  const configuredSeconds = Number(
    parsed.primary?.baseValues?.transition_seconds
    ?? parsed.primary?.baseValues?.transition_time
    ?? parsed.primary?.baseValues?.transitiontime
    ?? parsed.primary?.baseValues?.blend_seconds
    ?? parsed.primary?.baseValues?.blend_time
    ?? parsed.primary?.baseValues?.blendtime,
  );
  return Number.isFinite(configuredSeconds) && configuredSeconds >= 0
    ? configuredSeconds
    : fallback;
};

const getPresetSetTransitionMode = (parsed, fallback = defaultTransitionMode) =>
  normalizeTransitionMode(
    parsed.primary?.baseValues?.transition_mode
    ?? parsed.primary?.baseValues?.transitionmode
    ?? parsed.primary?.baseValues?.transition_style
    ?? parsed.primary?.baseValues?.transitionstyle
    ?? parsed.primary?.baseValues?.blend_transition
    ?? fallback,
  );

const createRendererSet = ({
  canvas,
  parsed,
  textureAssets,
  title,
  transitionMode = defaultTransitionMode,
  transitionSeconds = defaultTransitionSeconds,
  transitionStartedAt = 0,
}) => ({
  entries: parsed.presets.map((preset, index) => ({
    blendAlpha: getCompositeAlpha(preset, index),
    compositeMode: getCompositeMode(preset, index),
    renderer: createMilkdropRenderer({
      canvas,
      preset,
      textureAssets,
    }),
  })),
  title,
  transitionMode,
  transitionSeconds,
  transitionStartedAt,
});

const renderRendererSet = (rendererSet, renderFrame, alpha, clearFirstEntry) => {
  if (alpha <= 0) return;
  rendererSet.entries.forEach((entry, index) => {
    entry.renderer.render(renderFrame, {
      clearScreen: clearFirstEntry && index === 0,
      compositeMode: entry.compositeMode,
      outputAlpha: entry.blendAlpha * alpha,
    });
  });
};

export const createNativeMilkdropEngine = async ({
  audioContext,
  audioNode,
  canvas,
}) => {
  let presetIndex = 0;
  let activeParsedPresetSet = parseMilkdropPreset(nativePresets[presetIndex].source);
  let activePresetTitle = nativePresets[presetIndex].name;
  let activeRendererSet = createRendererSet({
    canvas,
    parsed: activeParsedPresetSet,
    title: activePresetTitle,
    transitionSeconds: 0,
  });
  let retiringRendererSets = [];
  let automation = normalizeAutomation();
  let beatState = {};
  let lastAutomatedPresetAt = 0;
  let mouseState = {
    mouse_down: 0,
    mouse_dx: 0,
    mouse_dy: 0,
    mouse_x: 0.5,
    mouse_y: 0.5,
  };
  const frameReader = createFrameReader(audioContext, audioNode);

  const pruneRetiredRenderers = (now) => {
    const retained = [];
    retiringRendererSets.forEach((rendererSet) => {
      const progress = getNativeMilkdropTransitionProgress(
        rendererSet.transitionStartedAt,
        rendererSet.transitionSeconds,
        now,
      );
      if (progress >= 1) {
        disposeRendererSet(rendererSet);
      } else {
        retained.push(rendererSet);
      }
    });
    retiringRendererSets = retained;
  };

  const activateRendererSet = (
    nextRendererSet,
    transitionSeconds = defaultTransitionSeconds,
    transitionMode = defaultTransitionMode,
  ) => {
    const startedAt = audioContext.currentTime || 0;
    const effectiveTransitionSeconds = Math.max(0, Number(transitionSeconds) || 0);
    const effectiveTransitionMode = normalizeTransitionMode(transitionMode);
    if (effectiveTransitionSeconds > 0 && effectiveTransitionMode !== 'cut') {
      retiringRendererSets.push({
        ...activeRendererSet,
        transitionMode: effectiveTransitionMode,
        transitionSeconds: effectiveTransitionSeconds,
        transitionStartedAt: startedAt,
      });
    } else {
      disposeRendererSet(activeRendererSet);
      disposeRendererSets(retiringRendererSets);
      retiringRendererSets = [];
    }
    activeRendererSet = {
      ...nextRendererSet,
      transitionMode: effectiveTransitionMode,
      transitionSeconds: effectiveTransitionSeconds,
      transitionStartedAt: startedAt,
    };
  };

  const loadPreset = (index, options = {}) => {
    presetIndex = index % nativePresets.length;
    activeParsedPresetSet = parseMilkdropPreset(nativePresets[presetIndex].source);
    activePresetTitle = nativePresets[presetIndex].name;
    activateRendererSet(
      createRendererSet({
        canvas,
        parsed: activeParsedPresetSet,
        title: activePresetTitle,
      }),
      options.blendSeconds ?? getPresetSetTransitionSeconds(activeParsedPresetSet),
      options.transitionMode ?? getPresetSetTransitionMode(activeParsedPresetSet),
    );
    return activePresetTitle;
  };

  const maybeAdvanceAutomatedPreset = (renderFrame, now) => {
    if (automation.mode === 'off') return null;
    if (automation.mode === 'timed') {
      if (now - lastAutomatedPresetAt < automation.timedIntervalSeconds) return null;
      lastAutomatedPresetAt = now;
      return loadPreset(presetIndex + 1);
    }

    const nextBeatState = getNativeMilkdropBeatUpdate(
      beatState,
      renderFrame.spectrum,
      now,
      automation,
    );
    beatState = nextBeatState;
    if (
      !nextBeatState.isBeat
      || nextBeatState.beatCount < automation.beatsPerPreset
      || now - lastAutomatedPresetAt < defaultTransitionSeconds
    ) {
      return null;
    }
    beatState = {
      ...nextBeatState,
      beatCount: 0,
    };
    lastAutomatedPresetAt = now;
    return loadPreset(presetIndex + 1);
  };

  return {
    name: 'slskdN MilkDrop WebGL',
    presetName: nativePresets[presetIndex].name,
    dispose: () => {
      frameReader.disconnect();
      disposeRendererSet(activeRendererSet);
      disposeRendererSets(retiringRendererSets);
      retiringRendererSets = [];
    },
    loadPresetText: (source, fileName = '', options = {}) => {
      const importedPresetSet = parseCompatiblePresetText(source, fileName);
      const title = getParsedPresetSetTitle(importedPresetSet, fileName);
      activeParsedPresetSet = importedPresetSet;
      activePresetTitle = title;
      activateRendererSet(
        createRendererSet({
          canvas,
          parsed: activeParsedPresetSet,
          textureAssets: options.textureAssets,
          title,
        }),
        options.blendSeconds ?? getPresetSetTransitionSeconds(activeParsedPresetSet),
        options.transitionMode ?? getPresetSetTransitionMode(activeParsedPresetSet),
      );
      return title;
    },
    loadPresetFragmentText: (source, fileName = '', options = {}) => {
      const fragment = parseMilkdropFragment(source, { fileName });
      const mergedPresetSet = mergeFragmentIntoPresetSet(activeParsedPresetSet, fragment);
      const compatibilityErrors = getCompatibilityErrors(mergedPresetSet);
      if (compatibilityErrors.length > 0) {
        throw new Error(formatCompatibilityError(mergedPresetSet, compatibilityErrors));
      }
      const title = `${activePresetTitle} + ${fileName || fragment.type}`;
      activeParsedPresetSet = mergedPresetSet;
      activePresetTitle = title;
      const mergedSource = serializeMilkdropPresetSet(activeParsedPresetSet);
      activateRendererSet(
        createRendererSet({
          canvas,
          parsed: activeParsedPresetSet,
          textureAssets: options.textureAssets,
          title,
        }),
        options.blendSeconds ?? getPresetSetTransitionSeconds(activeParsedPresetSet),
        options.transitionMode ?? getPresetSetTransitionMode(activeParsedPresetSet),
      );
      return {
        source: mergedSource,
        title,
      };
    },
    exportPresetFragment: (type = 'shape', index = 0) => {
      const entries = type === 'wave'
        ? activeParsedPresetSet.primary.waves
        : activeParsedPresetSet.primary.shapes;
      const entry = entries[index];
      if (!entry) return null;
      return {
        fileName: `${activePresetTitle.replace(/[^A-Za-z0-9._-]+/g, '_')}.${type}`,
        source: serializeMilkdropFragment(entry, { type }),
      };
    },
    exportPresetText: () => ({
      fileName: `${activePresetTitle.replace(/[^A-Za-z0-9._-]+/g, '_')}.${activeParsedPresetSet.format}`,
      source: serializeMilkdropPresetSet(activeParsedPresetSet),
    }),
    getPresetDebugSnapshot: () => getPresetDebugSnapshot(activeParsedPresetSet, activePresetTitle),
    getPresetFragmentSummary: () => getPresetFragmentSummary(activeParsedPresetSet),
    getPresetParameterSummary: () => getPresetParameterSummary(activeParsedPresetSet),
    inspectPresetText: (source, fileName = '') => {
      const importedPresetSet = parseCompatiblePresetText(source, fileName);
      return {
        title: getParsedPresetSetTitle(importedPresetSet, fileName),
      };
    },
    nextPreset: (options = {}) => loadPreset(presetIndex + 1, options),
    setPresetAutomation: (nextAutomation = {}) => {
      automation = normalizeAutomation(nextAutomation);
      beatState = {};
      lastAutomatedPresetAt = audioContext.currentTime || 0;
      return automation;
    },
    setMouseState: (nextMouseState = {}) => {
      mouseState = {
        ...mouseState,
        ...nextMouseState,
      };
      return mouseState;
    },
    removePresetFragment: (type = 'shape', index = 0, options = {}) => {
      const normalizedType = type === 'wave' ? 'wave' : 'shape';
      const mergedPresetSet = removeFragmentFromPresetSet(
        activeParsedPresetSet,
        normalizedType,
        index,
      );
      if (!mergedPresetSet) return null;
      const compatibilityErrors = getCompatibilityErrors(mergedPresetSet);
      if (compatibilityErrors.length > 0) {
        throw new Error(formatCompatibilityError(mergedPresetSet, compatibilityErrors));
      }
      const title = `${activePresetTitle} - ${normalizedType} ${index + 1}`;
      activeParsedPresetSet = mergedPresetSet;
      activePresetTitle = title;
      const mergedSource = serializeMilkdropPresetSet(activeParsedPresetSet);
      activateRendererSet(
        createRendererSet({
          canvas,
          parsed: activeParsedPresetSet,
          textureAssets: options.textureAssets,
          title,
        }),
        options.blendSeconds ?? getPresetSetTransitionSeconds(activeParsedPresetSet),
        options.transitionMode ?? getPresetSetTransitionMode(activeParsedPresetSet),
      );
      return {
        source: mergedSource,
        title,
      };
    },
    randomizePresetParameters: (options = {}) => {
      const randomizedValues = getRandomizedPresetParameters(activeParsedPresetSet, options.random);
      const updatedPresetSet = updatePresetBaseValues(activeParsedPresetSet, randomizedValues);
      const compatibilityErrors = getCompatibilityErrors(updatedPresetSet);
      if (compatibilityErrors.length > 0) {
        throw new Error(formatCompatibilityError(updatedPresetSet, compatibilityErrors));
      }
      const title = `${activePresetTitle} randomized`;
      activeParsedPresetSet = updatedPresetSet;
      activePresetTitle = title;
      const updatedSource = serializeMilkdropPresetSet(activeParsedPresetSet);
      activateRendererSet(
        createRendererSet({
          canvas,
          parsed: activeParsedPresetSet,
          textureAssets: options.textureAssets,
          title,
        }),
        options.blendSeconds ?? getPresetSetTransitionSeconds(activeParsedPresetSet),
        options.transitionMode ?? getPresetSetTransitionMode(activeParsedPresetSet),
      );
      return {
        source: updatedSource,
        title,
        values: getPresetParameterSummary(activeParsedPresetSet),
      };
    },
    updatePresetBaseValue: (key, value, options = {}) => {
      if (!editableParameterKeys.includes(key)) return null;
      const numericValue = Number(value);
      if (!Number.isFinite(numericValue)) return null;
      const updatedPresetSet = updatePresetBaseValue(activeParsedPresetSet, key, numericValue);
      const compatibilityErrors = getCompatibilityErrors(updatedPresetSet);
      if (compatibilityErrors.length > 0) {
        throw new Error(formatCompatibilityError(updatedPresetSet, compatibilityErrors));
      }
      const title = `${activePresetTitle} edited`;
      activeParsedPresetSet = updatedPresetSet;
      activePresetTitle = title;
      const updatedSource = serializeMilkdropPresetSet(activeParsedPresetSet);
      activateRendererSet(
        createRendererSet({
          canvas,
          parsed: activeParsedPresetSet,
          textureAssets: options.textureAssets,
          title,
        }),
        options.blendSeconds ?? getPresetSetTransitionSeconds(activeParsedPresetSet),
        options.transitionMode ?? getPresetSetTransitionMode(activeParsedPresetSet),
      );
      return {
        source: updatedSource,
        title,
        values: getPresetParameterSummary(activeParsedPresetSet),
      };
    },
    render: () => {
      const now = audioContext.currentTime || 0;
      pruneRetiredRenderers(now);
      const frame = frameReader.read();
      const renderFrame = {
        ...frame,
        audio: {
          ...mouseState,
        },
        sampleRate: audioContext.sampleRate,
        time: now,
      };
      const automatedPresetName = maybeAdvanceAutomatedPreset(renderFrame, now);

      let clearNextSet = true;
      retiringRendererSets.forEach((rendererSet) => {
        const progress = getNativeMilkdropTransitionProgress(
          rendererSet.transitionStartedAt,
          rendererSet.transitionSeconds,
          now,
        );
        const { outgoing } = getNativeMilkdropTransitionAlphas(
          progress,
          rendererSet.transitionMode,
        );
        renderRendererSet(rendererSet, renderFrame, outgoing, clearNextSet);
        if (outgoing > 0) {
          clearNextSet = false;
        }
      });

      const activeProgress = retiringRendererSets.length > 0
        ? getNativeMilkdropTransitionAlphas(
          getNativeMilkdropTransitionProgress(
            activeRendererSet.transitionStartedAt,
            activeRendererSet.transitionSeconds,
            now,
          ),
          activeRendererSet.transitionMode,
        ).incoming
        : 1;
      renderRendererSet(activeRendererSet, renderFrame, activeProgress, clearNextSet);
      return automatedPresetName ? { presetName: automatedPresetName } : null;
    },
    resize: (width, height) => {
      [activeRendererSet, ...retiringRendererSets].forEach((rendererSet) => {
        rendererSet.entries.forEach((entry) => entry.renderer.resize(width, height));
      });
    },
  };
};
