const numericPattern = /^[-+]?(?:\d+\.?\d*|\.\d+)(?:e[-+]?\d+)?$/i;
const sectionPattern = /^\s*\[([^\]]+)]\s*$/;
const keyedLinePattern = /^\s*([^=]+?)\s*=\s*(.*)$/;
const numberedExpressionPattern = /^(per_frame|per_pixel|per_vertex|per_point|init|frame|point)(?:_\d+)?$/i;
const shaderPattern = /^(warp|comp)_shader(?:_\d+)?$/i;
const shapePattern = /^shape(\d+)_(.+)$/i;
const wavePattern = /^wavecode_(\d+)_(.+)$/i;

const normalizeKey = (key) => key.trim().toLowerCase();

const normalizeValue = (value) => {
  const trimmed = value.trim();
  if (numericPattern.test(trimmed)) {
    return Number(trimmed);
  }
  return trimmed;
};

const appendStatement = (existing, next) => {
  if (!next) return existing || '';
  if (!existing) return next;
  return `${existing}\n${next}`;
};

const splitPresetPair = (text) => {
  const marker = /^\s*\[preset01]\s*$/im;
  const match = marker.exec(text);
  if (!match) return [text];
  return [
    text.slice(0, match.index),
    text.slice(match.index),
  ];
};

const createPreset = (source, index = 0) => ({
  baseValues: {},
  equations: {
    init: '',
    perFrame: '',
    perPixel: '',
  },
  index,
  metadata: {
    format: 'milk',
    title: '',
  },
  rawSections: {},
  shaders: {
    comp: '',
    warp: '',
  },
  shapes: [],
  waves: [],
  source,
});

const ensureIndexedEntry = (entries, index) => {
  while (entries.length <= index) {
    entries.push({
      baseValues: {},
      equations: {},
    });
  }
  return entries[index];
};

const assignEquation = (preset, key, value) => {
  const normalized = normalizeKey(key);
  if (normalized.startsWith('per_frame') || normalized.startsWith('frame')) {
    preset.equations.perFrame = appendStatement(preset.equations.perFrame, value);
    return true;
  }
  if (normalized.startsWith('per_pixel') || normalized.startsWith('per_vertex')) {
    preset.equations.perPixel = appendStatement(preset.equations.perPixel, value);
    return true;
  }
  if (normalized.startsWith('init')) {
    preset.equations.init = appendStatement(preset.equations.init, value);
    return true;
  }
  return numberedExpressionPattern.test(normalized);
};

const assignIndexedEquation = (entry, key, value) => {
  const normalized = normalizeKey(key);
  if (normalized.startsWith('init')) {
    entry.equations.init = appendStatement(entry.equations.init, value);
    return true;
  }
  if (normalized.startsWith('frame') || normalized.startsWith('per_frame')) {
    entry.equations.frame = appendStatement(entry.equations.frame, value);
    return true;
  }
  if (normalized.startsWith('point') || normalized.startsWith('per_point')) {
    entry.equations.point = appendStatement(entry.equations.point, value);
    return true;
  }
  return false;
};

const parsePresetText = (text, index = 0) => {
  const preset = createPreset(text, index);
  let section = '';

  text.replace(/\r\n?/g, '\n').split('\n').forEach((line) => {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith(';') || trimmed.startsWith('//')) {
      return;
    }

    const sectionMatch = sectionPattern.exec(line);
    if (sectionMatch) {
      section = normalizeKey(sectionMatch[1]);
      preset.rawSections[section] = {};
      return;
    }

    const keyedMatch = keyedLinePattern.exec(line);
    if (!keyedMatch) return;

    const key = normalizeKey(keyedMatch[1]);
    const rawValue = keyedMatch[2].trim();
    const value = normalizeValue(rawValue);
    const targetSection = section || 'preset';
    preset.rawSections[targetSection] = preset.rawSections[targetSection] || {};
    preset.rawSections[targetSection][key] = value;

    if (key === 'name' || key === 'preset_name') {
      preset.metadata.title = rawValue;
      return;
    }

    const shapeMatch = shapePattern.exec(key);
    if (shapeMatch) {
      const entry = ensureIndexedEntry(preset.shapes, Number(shapeMatch[1]));
      const shapeKey = shapeMatch[2];
      if (!assignIndexedEquation(entry, shapeKey, rawValue)) {
        entry.baseValues[shapeKey] = value;
      }
      return;
    }

    const waveMatch = wavePattern.exec(key);
    if (waveMatch) {
      const entry = ensureIndexedEntry(preset.waves, Number(waveMatch[1]));
      const waveKey = waveMatch[2];
      if (!assignIndexedEquation(entry, waveKey, rawValue)) {
        entry.baseValues[waveKey] = value;
      }
      return;
    }

    const shaderMatch = shaderPattern.exec(key);
    if (shaderMatch) {
      preset.shaders[shaderMatch[1]] = appendStatement(
        preset.shaders[shaderMatch[1]],
        rawValue,
      );
      return;
    }

    if (assignEquation(preset, key, rawValue)) {
      return;
    }

    preset.baseValues[key] = value;
  });

  return preset;
};

export const parseMilkdropPreset = (text, options = {}) => {
  const source = String(text || '');
  const chunks = splitPresetPair(source);
  const presets = chunks.map((chunk, index) => parsePresetText(chunk, index));
  const format = chunks.length > 1 || options.format === 'milk2' ? 'milk2' : 'milk';
  presets.forEach((preset) => {
    preset.metadata.format = format;
  });

  return {
    format,
    presets,
    primary: presets[0],
  };
};

export const normalizeMilkdropPresetForSnapshot = (preset) => ({
  baseValues: preset.baseValues,
  equations: preset.equations,
  format: preset.metadata.format,
  shaders: preset.shaders,
  shapeCount: preset.shapes.length,
  shapes: preset.shapes,
  title: preset.metadata.title,
  waveCount: preset.waves.length,
  waves: preset.waves,
});
