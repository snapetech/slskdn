import { analyzeMilkdropPresetCompatibility, getMilkdropCompatibilityError } from './presetCompatibility';
import { parseMilkdropPreset } from './presetParser';

const sumIndexedEntries = (entries = []) =>
  entries.filter((entry) =>
    Object.keys(entry?.baseValues || {}).length > 0
    || Object.keys(entry?.equations || {}).length > 0).length;

const mergeUnique = (values) => Array.from(new Set(values.filter(Boolean))).sort();

const getPresetMetrics = (preset) => ({
  shapeCount: sumIndexedEntries(preset.shapes),
  spriteCount: sumIndexedEntries(preset.sprites),
  waveCount: sumIndexedEntries(preset.waves),
});

const mergeMetrics = (metrics) => metrics.reduce(
  (summary, metric) => ({
    maxShapeCount: Math.max(summary.maxShapeCount, metric.shapeCount),
    maxSpriteCount: Math.max(summary.maxSpriteCount, metric.spriteCount),
    maxWaveCount: Math.max(summary.maxWaveCount, metric.waveCount),
    totalShapes: summary.totalShapes + metric.shapeCount,
    totalSprites: summary.totalSprites + metric.spriteCount,
    totalWaves: summary.totalWaves + metric.waveCount,
  }),
  {
    maxShapeCount: 0,
    maxSpriteCount: 0,
    maxWaveCount: 0,
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
    maxShapeCount: Math.max(summary.maxShapeCount, entry.metrics.maxShapeCount),
    maxSpriteCount: Math.max(summary.maxSpriteCount, entry.metrics.maxSpriteCount),
    maxWaveCount: Math.max(summary.maxWaveCount, entry.metrics.maxWaveCount),
    presetCount: summary.presetCount + entry.presetCount,
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
    maxShapeCount: 0,
    maxSpriteCount: 0,
    maxWaveCount: 0,
    presetCount: 0,
    supportedCount: 0,
    totalCount: 0,
    unsupportedCount: 0,
    unsupportedFunctions: [],
    unsupportedShaderSections: [],
  },
);
