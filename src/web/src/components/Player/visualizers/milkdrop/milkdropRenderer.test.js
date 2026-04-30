import {
  createMilkdropRenderer,
  createQRegisterScope,
  createShaderFftBins,
  createShapeFillColors,
  createShapeFillVertices,
  createShapeTextureUvs,
  createSpriteTextureUvs,
  createSpriteVertices,
  createRepeatedColors,
  createScreenBorderVertices,
  createShapeVertices,
  createCustomWaveVertices,
  createMotionVectorVertices,
  createWarpGridMesh,
  createWaveformVertices,
  evaluateShapeState,
  evaluateSpriteState,
  evaluateWaveState,
  extractQRegisters,
  getMilkdropFrameColor,
  getMilkdropWarpState,
  getMotionVectorColor,
  getScreenBorderColor,
  getShapeBorderColor,
  getShapeColor,
  getShapeFillEdgeColor,
  getShapeFillColor,
  getSpriteFillColor,
  getWaveColor,
  getWaveDrawMode,
  getWavePointSize,
  isShapeTextured,
  isSpriteEnabled,
} from './milkdropRenderer';
import { parseMilkdropPreset } from './presetParser';

const createFakeGl = () => ({
  ARRAY_BUFFER: 0x8892,
  BLEND: 0x0be2,
  CLAMP_TO_EDGE: 0x812f,
  COLOR_ATTACHMENT0: 0x8ce0,
  COLOR_BUFFER_BIT: 0x4000,
  COMPILE_STATUS: 0x8b81,
  FLOAT: 0x1406,
  FRAMEBUFFER: 0x8d40,
  FRAGMENT_SHADER: 0x8b30,
  LINEAR: 0x2601,
  LINES: 0x0001,
  LINE_STRIP: 0x0003,
  LINK_STATUS: 0x8b82,
  RGBA: 0x1908,
  STATIC_DRAW: 0x88e4,
  DYNAMIC_DRAW: 0x88e8,
  ONE: 1,
  ONE_MINUS_SRC_ALPHA: 0x0303,
  POINTS: 0x0000,
  SRC_ALPHA: 0x0302,
  TEXTURE0: 0x84c0,
  TEXTURE1: 0x84c1,
  TEXTURE_2D: 0x0de1,
  TEXTURE_MAG_FILTER: 0x2800,
  TEXTURE_MIN_FILTER: 0x2801,
  TEXTURE_WRAP_S: 0x2802,
  TEXTURE_WRAP_T: 0x2803,
  TRIANGLE_FAN: 0x0006,
  TRIANGLES: 0x0004,
  UNSIGNED_BYTE: 0x1401,
  VERTEX_SHADER: 0x8b31,
  activeTexture: vi.fn(),
  attachShader: vi.fn(),
  bindBuffer: vi.fn(),
  bindFramebuffer: vi.fn(),
  bindTexture: vi.fn(),
  blendFunc: vi.fn(),
  bufferData: vi.fn(),
  clear: vi.fn(),
  clearColor: vi.fn(),
  compileShader: vi.fn(),
  createBuffer: vi.fn(() => ({})),
  createFramebuffer: vi.fn(() => ({})),
  createProgram: vi.fn(() => ({})),
  createShader: vi.fn((type) => ({ type })),
  createTexture: vi.fn(() => ({})),
  deleteFramebuffer: vi.fn(),
  deleteProgram: vi.fn(),
  deleteTexture: vi.fn(),
  disable: vi.fn(),
  drawArrays: vi.fn(),
  enable: vi.fn(),
  enableVertexAttribArray: vi.fn(),
  framebufferTexture2D: vi.fn(),
  getAttribLocation: vi.fn(() => 0),
  getProgramInfoLog: vi.fn(() => ''),
  getProgramParameter: vi.fn(() => true),
  getShaderInfoLog: vi.fn(() => ''),
  getShaderParameter: vi.fn(() => true),
  getUniformLocation: vi.fn(() => ({})),
  linkProgram: vi.fn(),
  lineWidth: vi.fn(),
  shaderSource: vi.fn(),
  texImage2D: vi.fn(),
  texParameteri: vi.fn(),
  uniform1f: vi.fn(),
  uniform1fv: vi.fn(),
  uniform1i: vi.fn(),
  uniform2f: vi.fn(),
  uniform3f: vi.fn(),
  uniform4f: vi.fn(),
  useProgram: vi.fn(),
  vertexAttribPointer: vi.fn(),
  viewport: vi.fn(),
});

const createCanvas = (gl) => ({
  height: 64,
  width: 64,
  getContext: vi.fn((name) => (name === 'webgl2' ? gl : null)),
});

describe('native MilkDrop WebGL renderer skeleton', () => {
  it('evaluates preset frame equations and draws a GPU-backed frame color', () => {
    const gl = createFakeGl();
    const canvas = createCanvas(gl);
    const preset = parseMilkdropPreset(`
      wave_r=0.1
      wave_g=0.2
      wave_b=0.3
      per_frame_1=wave_r = min(1, wave_r + bass_att * 0.2);
      per_frame_2=wave_g = q33;
      per_frame_3=wave_b = above(treb_att, 1.5);
      per_frame_4=zoom = 1.2;
      per_frame_5=rot = 0.25;
      per_frame_6=dx = 0.01;
      per_frame_7=dy = -0.02;
      mv_x=2
      mv_y=1
      mv_dx=0.5
      mv_dy=0
      mv_l=0.1
      shape00_enabled=1
      shape00_sides=4
      shape00_rad=0.25
      shape00_r=1
      shape00_g=0.5
      shape00_b=0
      shape00_border_r=0
      shape00_border_g=1
      shape00_border_b=0.25
      shape00_border_a=1
      shape00_thickoutline=1
      shape00_init1=q1=0.1;
      shape00_per_frame1=rad=rad+q1;
      wavecode_0_enabled=1
      wavecode_0_samples=3
      wavecode_0_r=0.25
      wavecode_0_g=0.5
      wavecode_0_b=1
      wavecode_0_dots=1
      wavecode_0_thick=1
      wavecode_0_init1=q1=0.2;
      wavecode_0_per_frame1=a=q1+0.3;
      wavecode_0_per_point1=x=i;
      wavecode_0_per_point2=y=0.5+sample*0.25;
    `).primary;

    const renderer = createMilkdropRenderer({ canvas, preset });
    const scope = renderer.render({
      audio: {
        bass_att: 2,
        treb_att: 2,
      },
      frame: 1,
      samples: [-1, 0, 1],
      time: 0.25,
    });

    expect(scope.wave_r).toBeCloseTo(0.5);
    expect(scope.wave_g).toBe(0);
    expect(scope.wave_b).toBe(1);
    expect(scope.zoom).toBe(1.2);
    expect(scope.rot).toBe(0.25);
    expect(gl.uniform3f).toHaveBeenNthCalledWith(1, expect.anything(), 0.5, 0, 1);
    expect(gl.uniform1f).toHaveBeenNthCalledWith(1, expect.anything(), 0.9);
    expect(gl.uniform1f).toHaveBeenCalledWith(expect.anything(), 0.25);
    expect(gl.uniform1f).toHaveBeenCalledWith(expect.anything(), 1.2);
    expect(gl.uniform2f).toHaveBeenNthCalledWith(1, expect.anything(), 0.01, -0.02);
    expect(gl.bindFramebuffer).toHaveBeenCalledWith(
      gl.FRAMEBUFFER,
      expect.anything(),
    );
    expect(gl.bindFramebuffer).toHaveBeenCalledWith(gl.FRAMEBUFFER, null);
    expect(gl.createTexture).toHaveBeenCalledTimes(3);
    expect(gl.createFramebuffer).toHaveBeenCalledTimes(2);
    expect(gl.drawArrays).toHaveBeenCalledTimes(7);
    expect(gl.drawArrays).toHaveBeenCalledWith(gl.LINE_STRIP, 0, 3);
    expect(gl.drawArrays).toHaveBeenCalledWith(gl.LINES, 0, 4);
    expect(gl.drawArrays).toHaveBeenCalledWith(gl.POINTS, 0, 3);
    expect(gl.drawArrays).toHaveBeenCalledWith(gl.TRIANGLE_FAN, 0, 6);
    expect(gl.drawArrays).toHaveBeenCalledWith(gl.LINE_STRIP, 0, 5);
    expect(gl.drawArrays).toHaveBeenCalledWith(gl.TRIANGLES, 0, 3);
    expect(gl.enable).toHaveBeenCalledWith(gl.BLEND);
    expect(gl.blendFunc).toHaveBeenCalledWith(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
    expect(gl.disable).toHaveBeenCalledWith(gl.BLEND);
    expect(gl.lineWidth).toHaveBeenCalledWith(2);
    expect(gl.lineWidth).toHaveBeenCalledWith(1);
  });

  it('initializes and propagates q1-q64 registers across render stages', () => {
    const gl = createFakeGl();
    const canvas = createCanvas(gl);
    const preset = parseMilkdropPreset(`
      q64=0.64
      wave_r=0.4
      per_frame_1=q1=0.25;
      wavecode_0_enabled=1
      wavecode_0_samples=3
      wavecode_0_per_frame1=q2=q1+0.25;
      wavecode_0_per_point1=x=i;
      wavecode_0_per_point2=y=0.5;
      shape00_enabled=1
      shape00_sides=4
      shape00_rad=0.1
      shape00_per_frame1=q3=q2+0.25;
      sprite00_enabled=1
      sprite00_w=0.1
      sprite00_h=0.1
      sprite00_per_frame1=q4=q3+0.25;
    `).primary;

    const renderer = createMilkdropRenderer({ canvas, preset });
    const scope = renderer.render({
      frame: 1,
      samples: [0, 0, 0],
      time: 1,
    });

    expect(createQRegisterScope({ q64: 0.64 })).toEqual(expect.objectContaining({
      q1: 0,
      q64: 0.64,
    }));
    expect(extractQRegisters({ q4: 1, time: 9 })).toEqual({ q4: 1 });
    expect(scope.q1).toBeCloseTo(0.25);
    expect(scope.q2).toBeCloseTo(0.5);
    expect(scope.q3).toBeCloseTo(0.75);
    expect(scope.q4).toBeCloseTo(1);
    expect(scope.q64).toBeCloseTo(0.64);

    renderer.dispose();
  });

  it('uses preset decay as feedback blend and releases GPU resources', () => {
    const gl = createFakeGl();
    const renderer = createMilkdropRenderer({
      canvas: createCanvas(gl),
      preset: parseMilkdropPreset(`
        decay=0.25
        wave_r=1
      `).primary,
    });

    renderer.render();
    renderer.dispose();

    expect(gl.uniform1f).toHaveBeenNthCalledWith(1, expect.anything(), 0.25);
    expect(gl.deleteFramebuffer).toHaveBeenCalledTimes(2);
    expect(gl.deleteTexture).toHaveBeenCalledTimes(3);
    expect(gl.deleteProgram).toHaveBeenCalledTimes(4);
  });

  it('renders classic waveform placement and alpha from preset scope', () => {
    const gl = createFakeGl();
    const renderer = createMilkdropRenderer({
      canvas: createCanvas(gl),
      preset: parseMilkdropPreset(`
        wave_a=0.5
        wave_mode=2
        wave_x=0.25
        wave_scale=1
      `).primary,
    });

    renderer.render({
      samples: [-0.5, 0.5],
    });

    expect(gl.enable).toHaveBeenCalledWith(gl.BLEND);
    expect(gl.blendFunc).toHaveBeenCalledWith(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
    expect(gl.bufferData).toHaveBeenCalledWith(
      gl.ARRAY_BUFFER,
      expect.objectContaining({ length: 8 }),
      gl.DYNAMIC_DRAW,
    );
    expect(gl.drawArrays).toHaveBeenCalledWith(gl.LINE_STRIP, 0, 2);
  });

  it('draws configured inner and outer screen borders', () => {
    const gl = createFakeGl();
    const renderer = createMilkdropRenderer({
      canvas: createCanvas(gl),
      preset: parseMilkdropPreset(`
        ob_size=0.1
        ob_r=1
        ob_g=0.2
        ob_b=0.3
        ob_a=0.4
        ib_size=0.05
        ib_r=0.2
        ib_g=0.8
        ib_b=1
        ib_a=0.5
      `).primary,
    });

    renderer.render();

    const triangleDraws = gl.drawArrays.mock.calls.filter((call) =>
      call[0] === gl.TRIANGLES && call[2] === 24);

    expect(triangleDraws).toHaveLength(2);
    expect(gl.uniform1f).toHaveBeenCalledWith(expect.anything(), 1);
    expect(gl.bufferData).toHaveBeenCalledWith(
      gl.ARRAY_BUFFER,
      expect.objectContaining({ length: 96 }),
      gl.DYNAMIC_DRAW,
    );
  });

  it('draws a per-pixel warp grid when preset warp equations are present', () => {
    const gl = createFakeGl();
    const renderer = createMilkdropRenderer({
      canvas: createCanvas(gl),
      preset: parseMilkdropPreset(`
        meshx=2
        meshy=1
        per_pixel_1=dx=x*0.1;
        per_pixel_2=dy=y*0.2;
      `).primary,
    });

    renderer.render();

    expect(gl.drawArrays).toHaveBeenCalledWith(gl.TRIANGLES, 0, 12);
    expect(gl.bufferData).toHaveBeenCalledWith(
      gl.ARRAY_BUFFER,
      expect.objectContaining({ length: 24 }),
      gl.DYNAMIC_DRAW,
    );
  });

  it('compiles supported preset warp and comp shaders into render passes', () => {
    const gl = createFakeGl();
    const renderer = createMilkdropRenderer({
      canvas: createCanvas(gl),
      preset: parseMilkdropPreset(`
        wave_r=0.5
        q64=0.64
        per_frame_1=q1=0.25;
        warp_shader=ret = tex2D(sampler_main, uv).rgb * tex2D(sampler_noise, uv).rgb * vec3(q1, get_fft(0.5), aspect);
        comp_shader=ret = vec3(get_fft_hz(11025) + pixelSize.x, q64, treb + x + y);
      `).primary,
      textureAssets: {
        sampler_noise: {
          data: new Uint8Array([255, 255, 255, 255]),
          height: 1,
          width: 1,
        },
      },
    });

    renderer.render({
      audio: {
        bass_att: 0.9,
        treb: 0.3,
      },
      sampleRate: 44100,
      spectrum: [0, 128, 255, 64],
      time: 2,
    });

    const shaderSources = gl.shaderSource.mock.calls.map(([, source]) => source);
    expect(shaderSources.some((source) =>
      source.includes('texture(previousFrame, uv).rgb * texture(shaderTexture0, uv).rgb * vec3(q1, get_fft(0.5), aspect)'))).toBe(true);
    expect(shaderSources.some((source) => source.includes('uniform sampler2D shaderTexture0;')))
      .toBe(true);
    expect(shaderSources.some((source) =>
      source.includes('vec3 ret = vec3(vec3(get_fft_hz(11025) + pixelSize.x, q64, treb + x + y))'))).toBe(true);
    expect(gl.uniform1f).toHaveBeenCalledWith(expect.anything(), 2);
    expect(gl.uniform1f).toHaveBeenCalledWith(expect.anything(), 44100);
    expect(gl.uniform1f).toHaveBeenCalledWith(expect.anything(), 1);
    expect(gl.uniform1f).toHaveBeenCalledWith(expect.anything(), 0.25);
    expect(gl.uniform1f).toHaveBeenCalledWith(expect.anything(), 0.64);
    expect(gl.uniform1f).toHaveBeenCalledWith(expect.anything(), 0.9);
    expect(gl.uniform1f).toHaveBeenCalledWith(expect.anything(), 0.3);
    expect(gl.uniform2f).toHaveBeenCalledWith(expect.anything(), 64, 64);
    expect(gl.uniform2f).toHaveBeenCalledWith(expect.anything(), 1 / 64, 1 / 64);
    expect(gl.uniform4f).toHaveBeenCalledWith(expect.anything(), 64, 64, 1 / 64, 1 / 64);
    expect(gl.uniform1i).toHaveBeenCalledWith(expect.anything(), 2);
    expect(gl.activeTexture).toHaveBeenCalledWith(gl.TEXTURE0 + 2);
    expect(gl.uniform1fv).toHaveBeenCalledWith(
      expect.anything(),
      expect.objectContaining({ length: 32 }),
    );
    expect(Array.from(createShaderFftBins([0, 128, 255, 64])).slice(0, 4)).toEqual([
      0,
      0,
      0,
      0,
    ]);
    expect(createShaderFftBins([0, 128, 255, 64])[31]).toBeCloseTo(64 / 255);
    expect(gl.drawArrays).toHaveBeenCalledWith(gl.TRIANGLES, 0, 3);
  });

  it('renders textured shapes through the procedural texture path', () => {
    const gl = createFakeGl();
    const renderer = createMilkdropRenderer({
      canvas: createCanvas(gl),
      preset: parseMilkdropPreset(`
        shape00_enabled=1
        shape00_textured=1
        shape00_sides=4
        shape00_rad=0.2
        shape00_r=0.5
        shape00_g=0.75
        shape00_b=1
        shape00_a=0.6
        shape00_tex_zoom=1.25
        shape00_tex_ang=0.2
      `).primary,
    });

    renderer.render();

    expect(gl.activeTexture).toHaveBeenCalledWith(gl.TEXTURE1);
    expect(gl.uniform3f).toHaveBeenCalledWith(expect.anything(), 0.5, 0.75, 1);
    expect(gl.uniform1f).toHaveBeenCalledWith(expect.anything(), 0.6);
    expect(gl.drawArrays).toHaveBeenCalledWith(gl.TRIANGLE_FAN, 0, 6);
  });

  it('uses named texture assets for textured shapes when available', () => {
    const gl = createFakeGl();
    const textureData = new Uint8Array([
      255, 0, 0, 255,
      0, 255, 0, 255,
    ]);
    const renderer = createMilkdropRenderer({
      canvas: createCanvas(gl),
      preset: parseMilkdropPreset(`
        shape00_enabled=1
        shape00_texture=cover.png
        shape00_sides=3
        shape00_rad=0.2
      `).primary,
      textureAssets: {
        'cover.png': {
          data: textureData,
          height: 1,
          width: 2,
        },
      },
    });

    renderer.render();

    expect(gl.texImage2D).toHaveBeenCalledWith(
      gl.TEXTURE_2D,
      0,
      gl.RGBA,
      2,
      1,
      0,
      gl.RGBA,
      gl.UNSIGNED_BYTE,
      textureData,
    );
    expect(gl.drawArrays).toHaveBeenCalledWith(gl.TRIANGLE_FAN, 0, 5);
  });

  it('matches imported texture assets by preset path basename and stem', () => {
    const gl = createFakeGl();
    const textureData = new Uint8Array([
      255, 255, 255, 255,
      0, 0, 0, 255,
    ]);
    const renderer = createMilkdropRenderer({
      canvas: createCanvas(gl),
      preset: parseMilkdropPreset(`
        shape00_enabled=1
        shape00_texture='textures\\\\cover.png'
        shape00_sides=4
        shape00_rad=0.2
      `).primary,
      textureAssets: {
        cover: {
          data: textureData,
          height: 1,
          width: 2,
        },
      },
    });

    renderer.render();

    expect(gl.texImage2D).toHaveBeenCalledWith(
      gl.TEXTURE_2D,
      0,
      gl.RGBA,
      2,
      1,
      0,
      gl.RGBA,
      gl.UNSIGNED_BYTE,
      textureData,
    );
    expect(gl.drawArrays).toHaveBeenCalledWith(gl.TRIANGLE_FAN, 0, 6);
  });

  it('renders enabled sprites with named texture assets', () => {
    const gl = createFakeGl();
    const textureData = new Uint8Array([
      255, 255, 255, 255,
      32, 64, 128, 255,
    ]);
    const renderer = createMilkdropRenderer({
      canvas: createCanvas(gl),
      preset: parseMilkdropPreset(`
        sprite00_enabled=1
        sprite00_image=sprites/logo.png
        sprite00_x=0.5
        sprite00_y=0.5
        sprite00_w=0.2
        sprite00_h=0.1
        sprite00_r=0.75
        sprite00_g=0.5
        sprite00_b=0.25
        sprite00_a=0.6
      `).primary,
      textureAssets: {
        logo: {
          data: textureData,
          height: 1,
          width: 2,
        },
      },
    });

    renderer.render();

    expect(gl.texImage2D).toHaveBeenCalledWith(
      gl.TEXTURE_2D,
      0,
      gl.RGBA,
      2,
      1,
      0,
      gl.RGBA,
      gl.UNSIGNED_BYTE,
      textureData,
    );
    expect(gl.uniform3f).toHaveBeenCalledWith(expect.anything(), 0.75, 0.5, 0.25);
    expect(gl.uniform1f).toHaveBeenCalledWith(expect.anything(), 0.6);
    expect(gl.drawArrays).toHaveBeenCalledWith(gl.TRIANGLE_FAN, 0, 5);
  });

  it('uses additive blending for additive custom shapes', () => {
    const gl = createFakeGl();
    const renderer = createMilkdropRenderer({
      canvas: createCanvas(gl),
      preset: parseMilkdropPreset(`
        shape00_enabled=1
        shape00_sides=3
        shape00_rad=0.2
        shape00_additive=1
      `).primary,
    });

    renderer.render();

    expect(gl.blendFunc).toHaveBeenCalledWith(gl.SRC_ALPHA, gl.ONE);
  });

  it('resizes the canvas through the renderer boundary', () => {
    const canvas = createCanvas(createFakeGl());
    const renderer = createMilkdropRenderer({
      canvas,
      preset: parseMilkdropPreset('wave_r=1').primary,
    });

    renderer.resize(320.8, 180.2);

    expect(canvas.width).toBe(320);
    expect(canvas.height).toBe(180);
    expect(canvas.getContext('webgl2').texImage2D).toHaveBeenCalledWith(
      expect.anything(),
      0,
      expect.anything(),
      320,
      180,
      0,
      expect.anything(),
      expect.anything(),
      null,
    );
    expect(renderer.name).toBe('slskdN MilkDrop WebGL');
  });

  it('throws when WebGL2 is unavailable', () => {
    expect(() =>
      createMilkdropRenderer({
        canvas: createCanvas(null),
        preset: parseMilkdropPreset('').primary,
      }),
    ).toThrow('WebGL2 is required');
  });

  it('clamps frame colors to the browser color range', () => {
    expect(getMilkdropFrameColor({
      wave_b: 2,
      wave_g: 0.5,
      wave_r: -1,
    })).toEqual([0, 0.5, 1]);
  });

  it('maps waveform samples into line-strip clip-space vertices', () => {
    expect(Array.from(createWaveformVertices([-1, 0, 1], 1))).toEqual([
      -1, -1,
      0, 0,
      1, 1,
    ]);
    expect(Array.from(createWaveformVertices([2, -2], 1))).toEqual([
      -1, 1,
      1, -1,
    ]);
    expect(createWaveformVertices([0], 1)).toHaveLength(0);
  });

  it('maps classic waveform modes and placement into clip-space vertices', () => {
    const centered = Array.from(createWaveformVertices([-0.5, 0, 0.5], {
      mode: 1,
      scale: 0.5,
      wave_y: 0.25,
    }));
    const vertical = Array.from(createWaveformVertices([-0.5, 0.5], {
      mode: 2,
      wave_x: 0.25,
    }));
    const circular = Array.from(createWaveformVertices([0, 0], {
      mode: 3,
      wave_x: 0.5,
      wave_y: 0.5,
    }));
    const smoothed = Array.from(createWaveformVertices([0, 1], {
      mode: 1,
      smoothing: 0.5,
    }));

    expect(centered[0]).toBeCloseTo(-1);
    expect(centered[1]).toBeCloseTo(-0.75);
    expect(centered[3]).toBeCloseTo(-0.5);
    expect(centered[5]).toBeCloseTo(-0.25);
    expect(vertical).toHaveLength(4);
    expect(vertical[0]).toBeCloseTo(-1);
    expect(vertical[1]).toBeCloseTo(-1);
    expect(vertical[2]).toBeCloseTo(0);
    expect(vertical[3]).toBeCloseTo(1);
    expect(circular[0]).toBeCloseTo(0.35);
    expect(circular[1]).toBeCloseTo(0);
    expect(circular[2]).toBeCloseTo(0.35);
    expect(circular[3]).toBeCloseTo(0);
    expect(smoothed[3]).toBeCloseTo(0.5);
  });

  it('evaluates per-pixel warp equations into a textured grid mesh', () => {
    const mesh = createWarpGridMesh(
      {
        dx: 0,
        dy: 0,
        rot: 0,
        zoom: 1,
      },
      'dx=x*0.1; dy=y*0.2;',
      1,
      1,
    );

    expect(mesh.vertexCount).toBe(6);
    expect(Array.from(mesh.positions)).toEqual([
      -1, -1,
      -1, 1,
      1, -1,
      1, -1,
      -1, 1,
      1, 1,
    ]);
    expect(mesh.sourceUvs[0]).toBeCloseTo(0);
    expect(mesh.sourceUvs[1]).toBeCloseTo(0);
    expect(mesh.sourceUvs[4]).toBeCloseTo(1.1);
    expect(mesh.sourceUvs[5]).toBeCloseTo(0);
    expect(mesh.sourceUvs[10]).toBeCloseTo(1.1);
    expect(mesh.sourceUvs[11]).toBeCloseTo(1.2);
  });

  it('maps motion vector settings into line vertices and colors', () => {
    const vertices = Array.from(createMotionVectorVertices({
      mv_dx: 0.5,
      mv_dy: -0.25,
      mv_l: 0.1,
      mv_x: 2,
      mv_y: 1,
    }));

    expect(vertices).toHaveLength(8);
    expect(vertices[0]).toBeCloseTo(-1);
    expect(vertices[1]).toBeCloseTo(0);
    expect(vertices[2]).toBeCloseTo(-0.9);
    expect(vertices[3]).toBeCloseTo(-0.05);
    expect(vertices[4]).toBeCloseTo(1);
    expect(vertices[5]).toBeCloseTo(0);
    expect(createMotionVectorVertices({ mv_x: 0, mv_y: 3 })).toHaveLength(0);
    expect(getMotionVectorColor({
      mv_a: 2,
      mv_b: 0.3,
      mv_g: 0.2,
      mv_r: 0.1,
    }, [0.4, 0.5, 0.6])).toEqual([0.1, 0.2, 0.3, 1]);
  });

  it('maps screen border settings into filled rectangle rings', () => {
    const outer = Array.from(createScreenBorderVertices(0.1));
    const inner = Array.from(createScreenBorderVertices(0.05, 0.2));

    expect(outer).toHaveLength(48);
    [
      -1, -1,
      1, -1,
      -1, -0.8,
      -1, -0.8,
      1, -1,
      1, -0.8,
    ].forEach((value, index) => {
      expect(outer[index]).toBeCloseTo(value);
    });
    expect(inner[0]).toBeCloseTo(-0.8);
    expect(inner[2]).toBeCloseTo(0.8);
    expect(createScreenBorderVertices(0)).toHaveLength(0);
    expect(getScreenBorderColor({
      ib_a: 0.4,
      ib_b: 1.5,
      ib_g: 0.2,
      ib_r: -1,
    }, 'ib', [0.3, 0.4, 0.5])).toEqual([0, 0.2, 1, 0.4]);
    expect(getScreenBorderColor({}, 'ob', [0.3, 0.4, 0.5])).toEqual([0.3, 0.4, 0.5, 0]);
  });

  it('maps enabled shapes into closed line-strip vertices', () => {
    const vertices = Array.from(createShapeVertices({
      baseValues: {
        enabled: 1,
        rad: 0.5,
        sides: 4,
        x: 0.5,
        y: 0.5,
      },
    }));

    expect(vertices).toHaveLength(10);
    expect(vertices[0]).toBeCloseTo(vertices[8]);
    expect(vertices[1]).toBeCloseTo(vertices[9]);
    expect(createShapeVertices({ baseValues: { enabled: 0 } })).toHaveLength(0);
  });

  it('maps enabled shapes into triangle-fan fill vertices', () => {
    const vertices = Array.from(createShapeFillVertices({
      baseValues: {
        enabled: 1,
        rad: 0.5,
        sides: 4,
        x: 0.5,
        y: 0.5,
      },
    }));

    expect(vertices).toHaveLength(12);
    expect(vertices[0]).toBe(0);
    expect(vertices[1]).toBe(0);
    expect(vertices[2]).toBeCloseTo(vertices[10]);
    expect(vertices[3]).toBeCloseTo(vertices[11]);
    expect(createShapeFillVertices({ baseValues: { enabled: 0 } })).toHaveLength(0);
  });

  it('maps textured shape fill vertices into texture coordinates', () => {
    const shape = {
      baseValues: {
        enabled: 1,
        rad: 0.5,
        sides: 4,
        tex_ang: 0,
        tex_zoom: 1,
        textured: 1,
        x: 0.5,
        y: 0.5,
      },
    };
    const uvs = Array.from(createShapeTextureUvs(shape));

    expect(isShapeTextured(shape)).toBe(true);
    expect(isShapeTextured({ baseValues: { texture: 'fixture' } })).toBe(true);
    expect(isShapeTextured({ baseValues: { btextured: 1 } })).toBe(true);
    expect(isShapeTextured({ baseValues: {} })).toBe(false);
    expect(uvs).toHaveLength(12);
    expect(uvs[0]).toBe(0.5);
    expect(uvs[1]).toBe(0.5);
    expect(uvs[2]).toBeCloseTo(1);
    expect(uvs[3]).toBeCloseTo(0.5);
  });

  it('maps enabled sprites into textured quads', () => {
    const sprite = {
      baseValues: {
        enabled: 1,
        h: 0.1,
        w: 0.2,
        x: 0.5,
        y: 0.5,
      },
    };

    expect(isSpriteEnabled(sprite)).toBe(true);
    expect(isSpriteEnabled({ baseValues: { benabled: 1 } })).toBe(true);
    expect(isSpriteEnabled({ baseValues: { enabled: 0 } })).toBe(false);
    const vertices = Array.from(createSpriteVertices(sprite));
    [
      -0.2, -0.1,
      0.2, -0.1,
      0.2, 0.1,
      -0.2, 0.1,
      -0.2, -0.1,
    ].forEach((value, index) => {
      expect(vertices[index]).toBeCloseTo(value);
    });
    expect(Array.from(createSpriteTextureUvs(sprite))).toEqual([
      0, 1,
      1, 1,
      1, 0,
      0, 0,
      0, 1,
    ]);
    expect(getSpriteFillColor({
      baseValues: {
        a: 0.4,
        b: 0.3,
        g: 0.2,
        r: 0.1,
      },
    })).toEqual([0.1, 0.2, 0.3, 0.4]);
  });

  it('evaluates shape init and frame equations without leaking global scope', () => {
    const shape = {
      baseValues: {
        enabled: 1,
        r: 0.2,
        rad: 0.1,
      },
      equations: {
        frame: 'rad=rad+q1+bass_att*0.1; r=min(1,r+0.3);',
        init: 'q1=0.2;',
      },
    };

    const firstFrame = evaluateShapeState(shape, {
      bass_att: 2,
      time: 9,
    });

    expect(firstFrame.baseValues.rad).toBeCloseTo(0.5);
    expect(firstFrame.baseValues.r).toBeCloseTo(0.5);
    expect(firstFrame.baseValues.q1).toBeCloseTo(0.2);
    expect(firstFrame.baseValues.bass_att).toBeUndefined();
    expect(firstFrame.baseValues.time).toBeUndefined();

    const secondFrame = evaluateShapeState(shape, {
      bass_att: 1,
    });

    expect(secondFrame.baseValues.rad).toBeCloseTo(0.8);
    expect(secondFrame.baseValues.q1).toBeCloseTo(0.2);
  });

  it('evaluates sprite init and frame equations without leaking global scope', () => {
    const sprite = {
      baseValues: {
        enabled: 1,
        w: 0.1,
      },
      equations: {
        frame: 'w=w+q1+bass_att*0.1;',
        init: 'q1=0.2;',
      },
    };

    const evaluatedSprite = evaluateSpriteState(sprite, {
      bass_att: 2,
      time: 9,
    });

    expect(evaluatedSprite.baseValues.w).toBeCloseTo(0.5);
    expect(evaluatedSprite.baseValues.q1).toBeCloseTo(0.2);
    expect(evaluatedSprite.baseValues.bass_att).toBeUndefined();
    expect(evaluatedSprite.baseValues.time).toBeUndefined();
  });

  it('evaluates wave init, frame, and point equations into clip-space vertices', () => {
    const wave = {
      baseValues: {
        enabled: 1,
        samples: 3,
      },
      equations: {
        frame: 'a=q1+0.3;',
        init: 'q1=0.2;',
        point: 'x=i; y=0.5+sample*0.25;',
      },
    };

    const evaluatedWave = evaluateWaveState(wave, {
      bass_att: 3,
      time: 9,
    });
    const vertices = Array.from(createCustomWaveVertices(
      evaluatedWave,
      [-1, 0, 1],
      { bass_att: 3 },
    ));

    expect(evaluatedWave.baseValues.a).toBeCloseTo(0.5);
    expect(evaluatedWave.baseValues.q1).toBeCloseTo(0.2);
    expect(evaluatedWave.baseValues.bass_att).toBeUndefined();
    expect(evaluatedWave.baseValues.time).toBeUndefined();
    expect(vertices).toHaveLength(6);
    expect(vertices[0]).toBeCloseTo(-1);
    expect(vertices[1]).toBeCloseTo(-0.5);
    expect(vertices[2]).toBeCloseTo(0);
    expect(vertices[3]).toBeCloseTo(0);
    expect(vertices[4]).toBeCloseTo(1);
    expect(vertices[5]).toBeCloseTo(0.5);
  });

  it('uses spectrum samples and point mode for custom spectrum dot waves', () => {
    const gl = createFakeGl();
    const wave = {
      baseValues: {
        dots: 1,
        enabled: 1,
        samples: 3,
        spectrum: 1,
        thick: 1,
      },
      equations: {
        point: 'x=i; y=sample;',
      },
    };
    const vertices = Array.from(createCustomWaveVertices(wave, [0, 128, 255], {}));

    expect(vertices[0]).toBeCloseTo(-1);
    expect(vertices[1]).toBeCloseTo(-1);
    expect(vertices[2]).toBeCloseTo(0);
    expect(vertices[3]).toBeCloseTo((128 / 255) * 2 - 1);
    expect(vertices[4]).toBeCloseTo(1);
    expect(vertices[5]).toBeCloseTo(1);
    expect(getWaveDrawMode(wave, gl)).toBe(gl.POINTS);
    expect(getWavePointSize(wave)).toBe(4);
    expect(getWavePointSize({ baseValues: { dots: 1 } })).toBe(2);
    expect(getWavePointSize({ baseValues: { dots: 0 } })).toBe(1);
  });

  it('honors native MilkDrop custom wave and shape aliases', () => {
    const gl = createFakeGl();
    const nativeWave = {
      baseValues: {
        benabled: 1,
        bspectrum: 1,
        bdrawthick: 1,
        busedots: 1,
        nsamples: 4,
      },
      equations: {
        point: 'x=i; y=sample;',
      },
    };
    const nativeShape = {
      baseValues: {
        badditive: 1,
        benabled: 1,
        btextured: 1,
        numsides: 5,
        rad: 0.2,
        texang: 0,
        texzoom: 2,
        x: 0.5,
        y: 0.5,
      },
    };

    const vertices = Array.from(createCustomWaveVertices(nativeWave, [0, 64, 128, 255], {}));

    expect(vertices).toHaveLength(8);
    expect(vertices[1]).toBeCloseTo(-1);
    expect(vertices[3]).toBeCloseTo((64 / 255) * 2 - 1);
    expect(vertices[7]).toBeCloseTo(1);
    expect(getWaveDrawMode(nativeWave, gl)).toBe(gl.POINTS);
    expect(getWavePointSize(nativeWave)).toBe(4);
    expect(createShapeVertices(nativeShape)).toHaveLength(12);
    expect(isShapeTextured(nativeShape)).toBe(true);
    expect(createShapeTextureUvs(nativeShape)[2]).toBeCloseTo(0.75);
  });

  it('uses shape colors with frame-color fallback', () => {
    expect(getShapeColor({
      baseValues: {
        b: 2,
        g: 0.5,
        r: -1,
      },
    }, [0.1, 0.2, 0.3])).toEqual([0, 0.5, 1]);
    expect(getShapeColor({ baseValues: {} }, [0.1, 0.2, 0.3])).toEqual([
      0.1,
      0.2,
      0.3,
    ]);
    expect(getShapeFillColor({
      baseValues: {
        a: 2,
        b: 0.3,
        g: 0.2,
        r: 0.1,
      },
    }, [0.4, 0.5, 0.6])).toEqual([0.1, 0.2, 0.3, 1]);
    expect(getShapeFillEdgeColor({
      baseValues: {
        a2: 0.4,
        b2: 0.3,
        g2: 0.2,
        r2: 0.1,
      },
    }, [0.4, 0.5, 0.6])).toEqual([0.1, 0.2, 0.3, 0.4]);
    expect(getShapeBorderColor({
      baseValues: {
        border_a: 0.4,
        border_b: 0.3,
        border_g: 0.2,
        border_r: 0.1,
      },
    }, [0.4, 0.5, 0.6])).toEqual([0.1, 0.2, 0.3, 0.4]);
    expect(getWaveColor({
      baseValues: {
        a: 0.4,
        b: 0.3,
        g: 0.2,
        r: 0.1,
      },
    }, [0.4, 0.5, 0.6])).toEqual([0.1, 0.2, 0.3, 0.4]);
  });

  it('creates per-vertex colors for primitive and gradient shape fills', () => {
    const repeatedColors = Array.from(createRepeatedColors(2, [0.1, 0.2, 0.3, 0.4]));
    [
      0.1, 0.2, 0.3, 0.4,
      0.1, 0.2, 0.3, 0.4,
    ].forEach((value, index) => {
      expect(repeatedColors[index]).toBeCloseTo(value);
    });

    const colors = Array.from(createShapeFillColors({
      baseValues: {
        a: 0.5,
        a2: 0.9,
        b: 0.3,
        b2: 0.6,
        enabled: 1,
        g: 0.2,
        g2: 0.5,
        r: 0.1,
        r2: 0.4,
        rad: 0.5,
        sides: 3,
      },
    }));

    [0.1, 0.2, 0.3, 0.5].forEach((value, index) => {
      expect(colors[index]).toBeCloseTo(value);
    });
    [0.4, 0.5, 0.6, 0.9].forEach((value, index) => {
      expect(colors[index + 4]).toBeCloseTo(value);
    });
    [0.4, 0.5, 0.6, 0.9].forEach((value, index) => {
      expect(colors[colors.length - 4 + index]).toBeCloseTo(value);
    });
  });

  it('normalizes warp defaults and clamps invalid zoom', () => {
    expect(getMilkdropWarpState({
      dx: 0.02,
      dy: -0.01,
      rot: 0.5,
      zoom: 0,
    })).toEqual({
      dx: 0.02,
      dy: -0.01,
      rot: 0.5,
      zoom: 1,
    });
    expect(getMilkdropWarpState({})).toEqual({
      dx: 0,
      dy: 0,
      rot: 0,
      zoom: 1,
    });
  });
});
