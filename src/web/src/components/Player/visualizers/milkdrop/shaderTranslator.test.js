import {
  analyzeMilkdropShaderSupport,
  createTranslatedMilkdropFragmentShader,
  translateMilkdropShaderExpression,
} from './shaderTranslator';

describe('MilkDrop shader translator', () => {
  it('translates simple ret assignments into GLSL fragment shaders', () => {
    expect(translateMilkdropShaderExpression(
      'ret = tex2D(sampler_main, uv).rgb * vec3(0.5, 1.0, 0.25);',
    )).toBe('texture(previousFrame, uv).rgb * vec3(0.5, 1.0, 0.25)');

    const shader = createTranslatedMilkdropFragmentShader(
      'ret = saturate(vec3(uv.x, uv.y, sin(time)));',
    );

    expect(shader).toContain('uniform sampler2D previousFrame;');
    expect(shader).toContain('uniform float fftBins[32];');
    expect(shader).toContain('float get_fft(float position)');
    expect(shader).toContain('float get_fft_hz(float hz)');
    expect(shader).toContain('uniform float bass_att;');
    expect(shader).toContain('uniform float q64;');
    expect(shader).toContain('vec3 ret = vec3(clamp01(vec3(uv.x, uv.y, sin(time))));');
    expect(analyzeMilkdropShaderSupport('ret = vec3(color);').supported).toBe(true);
  });

  it('accepts q-register and audio variables in safe shader expressions', () => {
    const shader = createTranslatedMilkdropFragmentShader(
      'ret = vec3(q1, bass_att, treb);',
    );

    expect(shader).toContain('uniform float q1;');
    expect(shader).toContain('uniform float treb;');
    expect(shader).toContain('vec3 ret = vec3(vec3(q1, bass_att, treb));');
    expect(analyzeMilkdropShaderSupport('ret = vec3(q64, mid_att, bass);').supported).toBe(true);
  });

  it('accepts FFT helpers in safe shader expressions', () => {
    const shader = createTranslatedMilkdropFragmentShader(
      'ret = vec3(get_fft(0.25), get_fft_hz(1000), q1);',
    );

    expect(shader).toContain('vec3 ret = vec3(vec3(get_fft(0.25), get_fft_hz(1000), q1));');
    expect(analyzeMilkdropShaderSupport('ret = vec3(get_fft(0.5));').supported).toBe(true);
  });

  it('rejects shader bodies outside the safe first translation subset', () => {
    expect(translateMilkdropShaderExpression('for (;;) { ret = vec3(1.0); }')).toBe('');
    expect(translateMilkdropShaderExpression('ret = unknown[index];')).toBe('');
    expect(analyzeMilkdropShaderSupport('if (uv.x > 0.5) ret = vec3(1.0);').supported).toBe(false);
  });
});
