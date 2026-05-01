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
    expect(summary.supportedCount).toBe(5);
    expect(summary.unsupportedCount).toBe(1);
    expect(summary.maxQRegisterIndex).toBe(64);
    expect(summary.maxShapeCount).toBe(40);
    expect(summary.maxWaveCount).toBe(20);
    expect(summary.qRegisters).toEqual(['q1', 'q2', 'q16', 'q32', 'q33', 'q34', 'q48', 'q63', 'q64']);
    expect(summary.unsupportedShaderSections).toEqual(['comp_shader']);
    expect(summary.webGpuSupportedCount).toBe(5);
    expect(summary.webGpuUnsupportedCount).toBe(1);
    expect(summary.webGpuUnsupportedShaderSections).toEqual(['comp_shader']);
    expect(matrix.find((entry) => entry.id === 'milk2-double').presetCount).toBe(2);
    expect(matrix.find((entry) => entry.id === 'milkdrop3-q-registers').presetCount).toBe(2);
    expect(matrix.find((entry) => entry.id === 'milkdrop3-dense-primitives').metrics)
      .toEqual(expect.objectContaining({
        maxShapeCount: 40,
        maxWaveCount: 20,
      }));
  });

  it('tracks dense real-pack shape and wave count pressure', () => {
    const entry = buildMilkdropCompatibilityEntry({
      id: 'dense-pack-probe',
      source: createDensePresetSource(),
    });

    expect(entry.supported).toBe(true);
    expect(entry.webGpuSupported).toBe(true);
    expect(entry.metrics.maxShapeCount).toBe(40);
    expect(entry.metrics.maxWaveCount).toBe(20);
  });

  it('tracks q-register and WebGPU shader pressure across MilkDrop3-style preset bodies', () => {
    const entry = buildMilkdropCompatibilityEntry({
      format: 'milk2',
      id: 'q-register-pack-probe',
      source: `
        [preset00]
        q64=0.5
        per_frame_1=q1=q64+bass;
        wavecode_0_enabled=1
        wavecode_0_per_point1=y=q48+sample;
        [preset01]
        per_frame_1=q63=q1+treb;
        shape00_enabled=1
        shape00_per_frame1=q32=q63*0.5;
        comp_shader=ret = tex2D(album_art, uv).rgb * vec3(q32, get_fft(0.5), get_waveform(0.5));
      `,
    });

    expect(entry.supported).toBe(true);
    expect(entry.webGpuSupported).toBe(true);
    expect(entry.webGpuShaderSections).toEqual([]);
    expect(entry.metrics.maxQRegisterIndex).toBe(64);
    expect(entry.metrics.qRegisters).toEqual(['q1', 'q32', 'q48', 'q63', 'q64']);
  });

  it('reports WebGPU-only shader translation gaps separately from WebGL support', () => {
    const entry = buildMilkdropCompatibilityEntry({
      id: 'webgpu-shader-gap-probe',
      source: 'comp_shader=ret = q1 > 0.5 ? vec3(1.0) : vec3(0.0);',
    });

    expect(entry.supported).toBe(true);
    expect(entry.webGpuSupported).toBe(false);
    expect(entry.webGpuShaderSections).toEqual(['comp_shader']);
  });
});
