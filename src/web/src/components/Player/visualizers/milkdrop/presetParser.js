const numericPattern = /^[-+]?(?:\d+\.?\d*|\.\d+)(?:e[-+]?\d+)?$/i;
const sectionPattern = /^\s*\[([^\]]+)]\s*$/;
const keyedLinePattern = /^\s*([^=]+?)\s*=\s*(.*)$/;
const numberedExpressionPattern = /^(per_frame|per_pixel|per_vertex|per_point|init|frame|point)(?:_\d+)?$/i;
const shaderPattern = /^(warp|comp)_shader(?:_\d+)?$/i;
const shapePattern = /^shape(\d+)_(.+)$/i;
const spritePattern = /^sprite(\d+)_(.+)$/i;
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
  sprites: [],
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

const cloneIndexedEntry = (entry = {}) => ({
  baseValues: { ...(entry.baseValues || {}) },
  equations: { ...(entry.equations || {}) },
});

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

    const spriteMatch = spritePattern.exec(key);
    if (spriteMatch) {
      const entry = ensureIndexedEntry(preset.sprites, Number(spriteMatch[1]));
      const spriteKey = spriteMatch[2];
      if (!assignIndexedEquation(entry, spriteKey, rawValue)) {
        entry.baseValues[spriteKey] = value;
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

const parseStandaloneFragmentEntry = (text) => {
  const entry = {
    baseValues: {
      enabled: 1,
    },
    equations: {},
  };

  text.replace(/\r\n?/g, '\n').split('\n').forEach((line) => {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith(';') || trimmed.startsWith('//')) {
      return;
    }
    if (sectionPattern.test(line)) {
      return;
    }

    const keyedMatch = keyedLinePattern.exec(line);
    if (!keyedMatch) return;

    const key = normalizeKey(keyedMatch[1]);
    const rawValue = keyedMatch[2].trim();
    if (!assignIndexedEquation(entry, key, rawValue)) {
      entry.baseValues[key] = normalizeValue(rawValue);
    }
  });

  return entry;
};

const getFragmentType = (fileName = '', requestedType = '') => {
  if (requestedType === 'shape' || requestedType === 'wave') return requestedType;
  const normalizedFileName = fileName.toLowerCase();
  if (normalizedFileName.endsWith('.shape')) return 'shape';
  if (normalizedFileName.endsWith('.wave')) return 'wave';
  return 'shape';
};

export const parseMilkdropFragment = (text, options = {}) => {
  const source = String(text || '');
  const type = getFragmentType(options.fileName, options.type);
  const parsed = parseMilkdropPreset(source);
  const parsedEntries = type === 'wave'
    ? parsed.primary.waves
    : parsed.primary.shapes;
  const hasPrefixedEntries = parsedEntries.some((entry) =>
    Object.keys(entry?.baseValues || {}).length > 0
    || Object.keys(entry?.equations || {}).length > 0);
  const entries = hasPrefixedEntries
    ? parsedEntries.filter(Boolean).map(cloneIndexedEntry)
    : [parseStandaloneFragmentEntry(source)];

  return {
    entries,
    source,
    type,
  };
};

const formatScalarValue = (value) => String(value);

const appendEquationLines = (lines, key, equationText) => {
  String(equationText || '')
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .forEach((line, index) => {
      lines.push(`${key}_${index + 1}=${line}`);
    });
};

const appendBaseValueLines = (lines, values = {}, prefix = '') => {
  Object.keys(values).sort().forEach((key) => {
    lines.push(`${prefix}${key}=${formatScalarValue(values[key])}`);
  });
};

const appendIndexedEntryLines = (lines, prefix, entry = {}) => {
  appendBaseValueLines(lines, entry.baseValues, prefix);
  appendEquationLines(lines, `${prefix}init`, entry.equations?.init);
  appendEquationLines(lines, `${prefix}per_frame`, entry.equations?.frame);
  appendEquationLines(lines, `${prefix}per_point`, entry.equations?.point);
};

const formatIndex = (index) => String(index).padStart(2, '0');

const serializePreset = (preset, index, includeSection) => {
  const lines = [];
  if (includeSection) {
    lines.push(`[preset${formatIndex(index)}]`);
  }
  if (preset.metadata?.title) {
    lines.push(`name=${preset.metadata.title}`);
  }
  appendBaseValueLines(lines, preset.baseValues);
  appendEquationLines(lines, 'init', preset.equations?.init);
  appendEquationLines(lines, 'per_frame', preset.equations?.perFrame);
  appendEquationLines(lines, 'per_pixel', preset.equations?.perPixel);
  appendEquationLines(lines, 'warp_shader', preset.shaders?.warp);
  appendEquationLines(lines, 'comp_shader', preset.shaders?.comp);
  (preset.shapes || []).forEach((shape, shapeIndex) =>
    appendIndexedEntryLines(lines, `shape${formatIndex(shapeIndex)}_`, shape));
  (preset.sprites || []).forEach((sprite, spriteIndex) =>
    appendIndexedEntryLines(lines, `sprite${formatIndex(spriteIndex)}_`, sprite));
  (preset.waves || []).forEach((wave, waveIndex) =>
    appendIndexedEntryLines(lines, `wavecode_${waveIndex}_`, wave));
  return lines.join('\n');
};

export const serializeMilkdropPresetSet = (parsed) => {
  const includeSections = parsed.format === 'milk2' || parsed.presets.length > 1;
  return `${parsed.presets
    .map((preset, index) => serializePreset(preset, index, includeSections))
    .join('\n')}\n`;
};

export const serializeMilkdropFragment = (entry, options = {}) => {
  const type = getFragmentType('', options.type);
  const lines = [`[${type}]`];
  appendIndexedEntryLines(lines, '', entry);
  return `${lines.join('\n')}\n`;
};

export const normalizeMilkdropPresetForSnapshot = (preset) => ({
  baseValues: preset.baseValues,
  equations: preset.equations,
  format: preset.metadata.format,
  shaders: preset.shaders,
  shapeCount: preset.shapes.length,
  shapes: preset.shapes,
  spriteCount: preset.sprites.length,
  sprites: preset.sprites,
  title: preset.metadata.title,
  waveCount: preset.waves.length,
  waves: preset.waves,
});
