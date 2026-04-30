import {
  analyzeMilkdropPresetCompatibility,
  getMilkdropCompatibilityError,
} from './presetCompatibility';
import { parseMilkdropPreset } from './presetParser';

describe('MilkDrop preset compatibility analysis', () => {
  it('reports unsupported equation functions and shader sections before rendering', () => {
    const preset = parseMilkdropPreset(`
      per_frame_1=q1=megabuf(0);
      per_pixel_1=q2=sin(pi);
      warp_shader=shader_body { ret = tex2D(sampler_main, uv); }
      wavecode_0_enabled=1
      wavecode_0_per_point1=y=customcall(sample);
      shape00_enabled=1
      shape00_per_frame1=rad=rand(4);
    `).primary;

    const report = analyzeMilkdropPresetCompatibility(preset);

    expect(report.unsupportedFunctions).toEqual(['customcall', 'megabuf']);
    expect(report.shaderSections).toEqual(['warp_shader']);
    expect(getMilkdropCompatibilityError(report)).toBe(
      'Native MilkDrop preset has unsupported functions: customcall, megabuf; shader translation pending: warp_shader.',
    );
  });

  it('accepts supported native equation helpers', () => {
    const preset = parseMilkdropPreset(`
      per_frame_1=q1=rand(4)+get_fft(0.5)+atan2(1,0);
      per_frame_2=q2=band(7,3)+sigmoid(q1,2);
    `).primary;

    const report = analyzeMilkdropPresetCompatibility(preset);

    expect(report.unsupportedFunctions).toEqual([]);
    expect(report.shaderSections).toEqual([]);
    expect(getMilkdropCompatibilityError(report)).toBe('');
  });
});
