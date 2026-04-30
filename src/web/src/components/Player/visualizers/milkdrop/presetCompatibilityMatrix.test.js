import { describe, expect, it } from 'vitest';
import { nativeMilkdropFixturePack } from './presetFixtures';
import {
  buildMilkdropCompatibilityEntry,
  buildMilkdropCompatibilityMatrix,
  summarizeMilkdropCompatibilityMatrix,
} from './presetCompatibilityMatrix';

const createDensePresetSource = () => {
  const lines = ['name=Dense Compatibility Probe'];
  for (let index = 0; index < 40; index += 1) {
    const padded = String(index).padStart(2, '0');
    lines.push(`shape${padded}_enabled=1`);
    lines.push(`shape${padded}_sides=5`);
    lines.push(`shape${padded}_rad=0.1`);
  }
  for (let index = 0; index < 20; index += 1) {
    lines.push(`wavecode_${index}_enabled=1`);
    lines.push(`wavecode_${index}_samples=16`);
    lines.push(`wavecode_${index}_per_point1=x=i;`);
  }
  return lines.join('\n');
};

describe('MilkDrop compatibility matrix', () => {
  it('summarizes curated fixture compatibility and unsupported shader gaps', () => {
    const matrix = buildMilkdropCompatibilityMatrix(nativeMilkdropFixturePack);
    const summary = summarizeMilkdropCompatibilityMatrix(matrix);

    expect(summary.totalCount).toBe(nativeMilkdropFixturePack.length);
    expect(summary.supportedCount).toBe(3);
    expect(summary.unsupportedCount).toBe(1);
    expect(summary.unsupportedShaderSections).toEqual(['comp_shader']);
    expect(matrix.find((entry) => entry.id === 'milk2-double').presetCount).toBe(2);
  });

  it('tracks dense real-pack shape and wave count pressure', () => {
    const entry = buildMilkdropCompatibilityEntry({
      id: 'dense-pack-probe',
      source: createDensePresetSource(),
    });

    expect(entry.supported).toBe(true);
    expect(entry.metrics.maxShapeCount).toBe(40);
    expect(entry.metrics.maxWaveCount).toBe(20);
  });
});
