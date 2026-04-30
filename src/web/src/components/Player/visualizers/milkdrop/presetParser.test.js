import {
  normalizeMilkdropPresetForSnapshot,
  parseMilkdropPreset,
} from './presetParser';

const classicPreset = `
// comments are ignored
[preset00]
name=slskdN smoke preset
fRating=4.0
fGammaAdj=1.35
zoom=1.01
rot=0
per_frame_1=q1 = bass_att * 0.2;
per_frame_2=zoom = zoom + q1;
per_pixel_1=rot = rot + rad * 0.01;
warp_shader=shader_body {
warp_shader_1=  ret = texture(sampler_main, uv).xyz;
warp_shader_2=}
comp_shader=shader_body { ret = vec3(q1); }
shape00_enabled=1
shape00_sides=5
shape00_init1=q2=0;
shape00_per_frame1=q2=q2+0.1;
sprite00_enabled=1
sprite00_image=logo.png
sprite00_init1=q3=0.2;
sprite00_per_frame1=x=0.5+q3;
wavecode_0_enabled=1
wavecode_0_samples=512
wavecode_0_per_point1=x=sample;
`;

const doublePreset = `
[preset00]
name=left preset
zoom=1
[preset01]
name=right preset
zoom=0.9
per_frame_1=q33=treb_att;
`;

describe('parseMilkdropPreset', () => {
  it('parses classic preset base values, equations, shaders, shapes, and waves', () => {
    const parsed = parseMilkdropPreset(classicPreset);
    const snapshot = normalizeMilkdropPresetForSnapshot(parsed.primary);

    expect(parsed.format).toBe('milk');
    expect(snapshot.title).toBe('slskdN smoke preset');
    expect(snapshot.baseValues).toEqual({
      fgammaadj: 1.35,
      frating: 4,
      rot: 0,
      zoom: 1.01,
    });
    expect(snapshot.equations.perFrame).toBe(
      'q1 = bass_att * 0.2;\nzoom = zoom + q1;',
    );
    expect(snapshot.equations.perPixel).toBe('rot = rot + rad * 0.01;');
    expect(snapshot.shaders.warp).toContain('texture(sampler_main, uv)');
    expect(snapshot.shaders.comp).toBe('shader_body { ret = vec3(q1); }');
    expect(snapshot.shapes[0].baseValues).toEqual({ enabled: 1, sides: 5 });
    expect(snapshot.shapes[0].equations).toEqual({
      frame: 'q2=q2+0.1;',
      init: 'q2=0;',
    });
    expect(snapshot.sprites[0].baseValues).toEqual({ enabled: 1, image: 'logo.png' });
    expect(snapshot.sprites[0].equations).toEqual({
      frame: 'x=0.5+q3;',
      init: 'q3=0.2;',
    });
    expect(snapshot.waves[0].baseValues).toEqual({ enabled: 1, samples: 512 });
    expect(snapshot.waves[0].equations.point).toBe('x=sample;');
  });

  it('recognizes MilkDrop3 double-preset files and preserves q33-q64 equations', () => {
    const parsed = parseMilkdropPreset(doublePreset);

    expect(parsed.format).toBe('milk2');
    expect(parsed.presets).toHaveLength(2);
    expect(parsed.presets[0].metadata.title).toBe('left preset');
    expect(parsed.presets[1].metadata.title).toBe('right preset');
    expect(parsed.presets[1].equations.perFrame).toBe('q33=treb_att;');
    expect(parsed.presets[1].metadata.format).toBe('milk2');
  });
});
