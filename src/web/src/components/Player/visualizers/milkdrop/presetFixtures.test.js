import {
  getMilkdropCompatibilityError,
  analyzeMilkdropPresetCompatibility,
} from './presetCompatibility';
import { nativeMilkdropFixturePack } from './presetFixtures';
import {
  normalizeMilkdropPresetForSnapshot,
  parseMilkdropPreset,
} from './presetParser';

const summarizePreset = (preset) => {
  const snapshot = normalizeMilkdropPresetForSnapshot(preset);
  return {
    baseValueKeys: Object.keys(snapshot.baseValues).sort(),
    format: snapshot.format,
    hasCompShader: Boolean(snapshot.shaders.comp),
    hasPerFrame: Boolean(snapshot.equations.perFrame),
    hasPerPixel: Boolean(snapshot.equations.perPixel),
    hasWarpShader: Boolean(snapshot.shaders.warp),
    shapeCount: snapshot.shapeCount,
    spriteCount: snapshot.spriteCount,
    title: snapshot.title,
    waveCount: snapshot.waveCount,
  };
};

describe('native MilkDrop fixture pack', () => {
  it('keeps golden parser summaries for curated fixtures', () => {
    const summaries = Object.fromEntries(
      nativeMilkdropFixturePack.map((fixture) => {
        const parsed = parseMilkdropPreset(fixture.source, {
          format: fixture.id === 'milk2-double' ? 'milk2' : undefined,
        });
        return [fixture.id, {
          format: parsed.format,
          presets: parsed.presets.map(summarizePreset),
        }];
      }),
    );

    expect(summaries).toEqual({
      'classic-primitives': {
        format: 'milk',
        presets: [{
          baseValueKeys: [
            'decay',
            'meshx',
            'meshy',
            'mv_a',
            'mv_l',
            'mv_x',
            'mv_y',
            'wave_b',
            'wave_g',
            'wave_r',
            'wave_scale',
          ],
          format: 'milk',
          hasCompShader: false,
          hasPerFrame: true,
          hasPerPixel: true,
          hasWarpShader: false,
          shapeCount: 1,
          spriteCount: 1,
          title: 'Fixture Classic Primitives',
          waveCount: 1,
        }],
      },
      'milk2-double': {
        format: 'milk2',
        presets: [
          {
            baseValueKeys: ['zoom'],
            format: 'milk2',
            hasCompShader: false,
            hasPerFrame: true,
            hasPerPixel: false,
            hasWarpShader: false,
            shapeCount: 0,
            spriteCount: 0,
            title: 'Fixture Double Left',
            waveCount: 0,
          },
          {
            baseValueKeys: ['blend_alpha', 'zoom'],
            format: 'milk2',
            hasCompShader: false,
            hasPerFrame: true,
            hasPerPixel: false,
            hasWarpShader: false,
            shapeCount: 0,
            spriteCount: 0,
            title: 'Fixture Double Right',
            waveCount: 0,
          },
        ],
      },
      'milkdrop3-q-registers': {
        format: 'milk2',
        presets: [
          {
            baseValueKeys: ['q64'],
            format: 'milk2',
            hasCompShader: false,
            hasPerFrame: true,
            hasPerPixel: false,
            hasWarpShader: true,
            shapeCount: 0,
            spriteCount: 0,
            title: 'Fixture Q Register Coverage A',
            waveCount: 1,
          },
          {
            baseValueKeys: ['blend_alpha', 'composite_mode'],
            format: 'milk2',
            hasCompShader: true,
            hasPerFrame: true,
            hasPerPixel: false,
            hasWarpShader: false,
            shapeCount: 1,
            spriteCount: 1,
            title: 'Fixture Q Register Coverage B',
            waveCount: 0,
          },
        ],
      },
      'shader-subset': {
        format: 'milk',
        presets: [{
          baseValueKeys: ['decay', 'wave_b', 'wave_g', 'wave_r'],
          format: 'milk',
          hasCompShader: true,
          hasPerFrame: false,
          hasPerPixel: false,
          hasWarpShader: true,
          shapeCount: 1,
          spriteCount: 0,
          title: 'Fixture Shader Subset',
          waveCount: 0,
        }],
      },
      'unsupported-control-flow-shader': {
        format: 'milk',
        presets: [{
          baseValueKeys: ['wave_r'],
          format: 'milk',
          hasCompShader: true,
          hasPerFrame: false,
          hasPerPixel: false,
          hasWarpShader: false,
          shapeCount: 0,
          spriteCount: 0,
          title: 'Fixture Unsupported Shader',
          waveCount: 0,
        }],
      },
    });
  });

  it('matches fixture support expectations through compatibility analysis', () => {
    const results = nativeMilkdropFixturePack.map((fixture) => {
      const parsed = parseMilkdropPreset(fixture.source, {
        format: fixture.id === 'milk2-double' ? 'milk2' : undefined,
      });
      const error = getMilkdropCompatibilityError(
        analyzeMilkdropPresetCompatibility(parsed.primary),
      );
      return {
        error,
        id: fixture.id,
      };
    });

    expect(results).toEqual([
      { error: '', id: 'classic-primitives' },
      { error: '', id: 'shader-subset' },
      { error: '', id: 'milk2-double' },
      { error: '', id: 'milkdrop3-q-registers' },
      {
        error: 'Native MilkDrop preset has shader translation pending: comp_shader.',
        id: 'unsupported-control-flow-shader',
      },
    ]);
  });
});
