import { analyzeMilkdropPresetCompatibility, getMilkdropCompatibilityError } from './presetCompatibility';
import { parseMilkdropPreset } from './presetParser';

const sumIndexedEntries = (entries = []) =>
  entries.filter((entry) =>
    Object.keys(entry?.baseValues || {}).length > 0
    || Object.keys(entry?.equations || {}).length > 0).length;

const mergeUnique = (values) => Array.from(new Set(values.filter(Boolean))).sort();

const qRegisterPattern = /\bq([1-9]|[1-5]\d|6[0-4])\b/gi;

const collectQRegistersFromText = (text, registers) => {
  let match = qRegisterPattern.exec(text || '');
  while (match) {
    registers.add(`q${Number(match[1])}`);
    match = qRegisterPattern.exec(text || '');
  }
};

const collectQRegistersFromEquations = (equations = {}, registers) => {
  Object.values(equations).forEach((text) => collectQRegistersFromText(text, registers));
};

const collectQRegisters = (preset) => {
  const registers = new Set();
  Object.keys(preset.baseValues || {}).forEach((key) => collectQRegistersFromText(key, registers));
  collectQRegistersFromEquations(preset.equations, registers);
  collectQRegistersFromText(preset.shaders?.warp, registers);
  collectQRegistersFromText(preset.shaders?.comp, registers);
  (preset.shapes || []).forEach((shape) => {
    Object.keys(shape.baseValues || {}).forEach((key) => collectQRegistersFromText(key, registers));
    collectQRegistersFromEquations(shape.equations, registers);
  });
  (preset.sprites || []).forEach((sprite) => {
    Object.keys(sprite.baseValues || {}).forEach((key) => collectQRegistersFromText(key, registers));
    collectQRegistersFromEquations(sprite.equations, registers);
  });
  (preset.waves || []).forEach((wave) => {
    Object.keys(wave.baseValues || {}).forEach((key) => collectQRegistersFromText(key, registers));
    collectQRegistersFromEquations(wave.equations, registers);
  });
  return Array.from(registers).sort((left, right) =>
    Number(left.slice(1)) - Number(right.slice(1)));
};

const getMaxQRegisterIndex = (registers = []) =>
  registers.reduce((max, register) => Math.max(max, Number(register.slice(1))), 0);

const getPresetMetrics = (preset) => ({
  qRegisterCount: collectQRegisters(preset).length,
  qRegisters: collectQRegisters(preset),
  shapeCount: sumIndexedEntries(preset.shapes),
  spriteCount: sumIndexedEntries(preset.sprites),
  waveCount: sumIndexedEntries(preset.waves),
});

const mergeMetrics = (metrics) => metrics.reduce(
  (summary, metric) => ({
    maxQRegisterIndex: Math.max(summary.maxQRegisterIndex, getMaxQRegisterIndex(metric.qRegisters)),
    maxShapeCount: Math.max(summary.maxShapeCount, metric.shapeCount),
    maxSpriteCount: Math.max(summary.maxSpriteCount, metric.spriteCount),
    maxWaveCount: Math.max(summary.maxWaveCount, metric.waveCount),
    qRegisters: mergeUnique([...summary.qRegisters, ...metric.qRegisters])
      .sort((left, right) => Number(left.slice(1)) - Number(right.slice(1))),
    totalShapes: summary.totalShapes + metric.shapeCount,
    totalSprites: summary.totalSprites + metric.spriteCount,
    totalWaves: summary.totalWaves + metric.waveCount,
  }),
  {
    maxQRegisterIndex: 0,
    maxShapeCount: 0,
    maxSpriteCount: 0,
    maxWaveCount: 0,
    qRegisters: [],
    totalShapes: 0,
    totalSprites: 0,
    totalWaves: 0,
  },
);

export const buildMilkdropCompatibilityEntry = ({
  fileName = '',
  format,
  id = fileName || 'preset',
  source = '',
} = {}) => {
  const parsed = parseMilkdropPreset(source, { format });
  const presetReports = parsed.presets.map((preset) => {
    const report = analyzeMilkdropPresetCompatibility(preset);
    return {
      error: getMilkdropCompatibilityError(report),
      index: preset.index,
      metrics: getPresetMetrics(preset),
      shaderSections: report.shaderSections,
      title: preset.metadata?.title || '',
      unsupportedFunctions: report.unsupportedFunctions,
    };
  });
  const errors = presetReports.map((report) => report.error).filter(Boolean);

  return {
    fileName,
    format: parsed.format,
    id,
    metrics: mergeMetrics(presetReports.map((report) => report.metrics)),
    presetCount: parsed.presets.length,
    presetReports,
    shaderSections: mergeUnique(presetReports.flatMap((report) => report.shaderSections)),
    supported: errors.length === 0,
    unsupportedFunctions: mergeUnique(
      presetReports.flatMap((report) => report.unsupportedFunctions),
    ),
  };
};

export const buildMilkdropCompatibilityMatrix = (sources = []) =>
  sources.map((source) => buildMilkdropCompatibilityEntry(source));

export const summarizeMilkdropCompatibilityMatrix = (entries = []) => entries.reduce(
  (summary, entry) => ({
    maxQRegisterIndex: Math.max(summary.maxQRegisterIndex, entry.metrics.maxQRegisterIndex),
    maxShapeCount: Math.max(summary.maxShapeCount, entry.metrics.maxShapeCount),
    maxSpriteCount: Math.max(summary.maxSpriteCount, entry.metrics.maxSpriteCount),
    maxWaveCount: Math.max(summary.maxWaveCount, entry.metrics.maxWaveCount),
    presetCount: summary.presetCount + entry.presetCount,
    qRegisters: mergeUnique([...summary.qRegisters, ...entry.metrics.qRegisters])
      .sort((left, right) => Number(left.slice(1)) - Number(right.slice(1))),
    supportedCount: summary.supportedCount + (entry.supported ? 1 : 0),
    totalCount: summary.totalCount + 1,
    unsupportedCount: summary.unsupportedCount + (entry.supported ? 0 : 1),
    unsupportedFunctions: mergeUnique([
      ...summary.unsupportedFunctions,
      ...entry.unsupportedFunctions,
    ]),
    unsupportedShaderSections: mergeUnique([
      ...summary.unsupportedShaderSections,
      ...entry.shaderSections,
    ]),
  }),
  {
    maxQRegisterIndex: 0,
    maxShapeCount: 0,
    maxSpriteCount: 0,
    maxWaveCount: 0,
    presetCount: 0,
    qRegisters: [],
    supportedCount: 0,
    totalCount: 0,
    unsupportedCount: 0,
    unsupportedFunctions: [],
    unsupportedShaderSections: [],
  },
);
