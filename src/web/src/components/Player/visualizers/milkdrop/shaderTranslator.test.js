import {
  analyzeMilkdropShaderSupport,
  createTranslatedMilkdropFragmentShader,
  getMilkdropShaderTextureSamplers,
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
    expect(shader).toContain('uniform vec2 resolution;');
    expect(shader).toContain('uniform vec2 pixelSize;');
    expect(shader).toContain('uniform float aspect;');
    expect(shader).toContain('uniform vec4 texsize;');
    expect(shader).toContain('float rad = length(centeredUv);');
    expect(shader).toContain('float ang = atan(centeredUv.y, centeredUv.x);');
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

  it('accepts straight-line temp declarations and common HLSL helper aliases', () => {
    const shader = createTranslatedMilkdropFragmentShader(`
      float2 shifted = uv + float2(frac(time), fmod(time, 1.0));
      float3 tinted = lerp(color, tex2D(sampler_main, shifted).rgb, 0.25);
      float energy = rsqrt(max(get_fft(0.25), 0.001));
      ret = tinted * vec3(energy, atan2(shifted.y, shifted.x), 1.0);
    `);

    expect(shader).toContain('vec2 shifted = uv + vec2(fract(time), mod(time, 1.0));');
    expect(shader).toContain('vec3 tinted = mix(color, texture(previousFrame, shifted).rgb, 0.25);');
    expect(shader).toContain('float energy = inversesqrt(max(get_fft(0.25), 0.001));');
    expect(shader).toContain('vec3 ret = vec3(tinted * vec3(energy, atan(shifted.y, shifted.x), 1.0));');
    expect(translateMilkdropShaderExpression('float3 tint = vec3(1.0); ret = tint;')).toBe('tint');
    expect(analyzeMilkdropShaderSupport('float2 p = uv; ret = vec3(p, 1.0);').supported).toBe(true);
  });

  it('accepts viewport and MilkDrop coordinate helpers in shader expressions', () => {
    const shader = createTranslatedMilkdropFragmentShader(`
      float2 pixel = pixelSize * texsize.xy;
      ret = vec3(x * aspect + pixel.x, y + resolution.y * texsize.w, rad + ang * 0.01);
    `);

    expect(shader).toContain('vec2 pixelSize;');
    expect(shader).toContain('vec4 texsize;');
    expect(shader).toContain('float x = uv.x;');
    expect(shader).toContain('vec2 pixel = pixelSize * texsize.xy;');
    expect(shader).toContain(
      'vec3 ret = vec3(vec3(x * aspect + pixel.x, y + resolution.y * texsize.w, rad + ang * 0.01));',
    );
    expect(analyzeMilkdropShaderSupport('ret = vec3(x, y, aspect);').supported).toBe(true);
  });

  it('unwraps safe shader_body blocks from imported presets', () => {
    const shader = createTranslatedMilkdropFragmentShader(`
      shader_body {
        float3 tint = saturate(vec3(x, y, aspect));
        ret = tint * tex2D(sampler_main, uv).rgb;
      }
    `);

    expect(shader).toContain('vec3 tint = clamp01(vec3(x, y, aspect));');
    expect(shader).toContain('vec3 ret = vec3(tint * texture(previousFrame, uv).rgb);');
    expect(translateMilkdropShaderExpression('shader_body { ret = vec3(q1); }')).toBe('vec3(q1)');
    expect(analyzeMilkdropShaderSupport('shader_body { ret = vec3(x, y, rad); }').supported)
      .toBe(true);
  });

  it('accepts capped named texture samplers in safe shader expressions', () => {
    const shader = createTranslatedMilkdropFragmentShader(`
      float3 noise = tex2D(sampler_noise, uv).rgb;
      float3 overlay = tex2D(album_art, uv).rgb;
      ret = noise * 0.5 + overlay * 0.5 + tex2D(sampler_main, uv).rgb * 0.1;
    `);

    expect(getMilkdropShaderTextureSamplers(
      'ret = tex2D(sampler_noise, uv).rgb + tex2D(album_art, uv).rgb;',
    )).toEqual(['sampler_noise', 'album_art']);
    expect(shader).toContain('uniform sampler2D shaderTexture0;');
    expect(shader).toContain('uniform sampler2D shaderTexture1;');
    expect(shader).toContain('vec3 noise = texture(shaderTexture0, uv).rgb;');
    expect(shader).toContain('vec3 overlay = texture(shaderTexture1, uv).rgb;');
    expect(shader).toContain('texture(previousFrame, uv).rgb * 0.1');
    expect(analyzeMilkdropShaderSupport('ret = tex2D(sampler_noise, uv).rgb;').supported)
      .toBe(true);
  });

  it('rejects shader bodies outside the safe first translation subset', () => {
    expect(translateMilkdropShaderExpression('for (;;) { ret = vec3(1.0); }')).toBe('');
    expect(translateMilkdropShaderExpression('ret = unknown[index];')).toBe('');
    expect(translateMilkdropShaderExpression('float3 tint; ret = tint;')).toBe('');
    expect(translateMilkdropShaderExpression('float3 tint = vec3(1.0); tint = tint * 0.5; ret = tint;')).toBe('');
    expect(analyzeMilkdropShaderSupport('if (uv.x > 0.5) ret = vec3(1.0);').supported).toBe(false);
  });
});
