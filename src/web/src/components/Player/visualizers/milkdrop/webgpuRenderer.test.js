import { describe, expect, it, vi } from 'vitest';
import {
  createMilkdropWebGpuRenderer,
  createWebGpuLineListVertices,
  createWebGpuLineSegmentVertices,
  createWebGpuMotionVectorVertices,
  createWebGpuScreenBorderVertices,
  createWebGpuShapeFillVertices,
  createWebGpuShapeOutlineVertices,
  createWebGpuSpriteVertices,
  createWebGpuTexturedShapeVertices,
  createWebGpuTexturedSpriteVertices,
  createWebGpuTexturedTriangleFanVertices,
  createWebGpuTriangleFanVertices,
  createWebGpuTriangleListVertices,
  getMilkdropWebGpuStatus,
} from './webgpuRenderer';

const createFakeWebGpu = () => {
  const pass = {
    draw: vi.fn(),
    end: vi.fn(),
    setBindGroup: vi.fn(),
    setPipeline: vi.fn(),
    setVertexBuffer: vi.fn(),
  };
  const encoder = {
    beginRenderPass: vi.fn(() => pass),
    finish: vi.fn(() => 'command-buffer'),
  };
  const uniformBuffer = {
    destroy: vi.fn(),
  };
  const filledPrimitiveBuffer = {
    destroy: vi.fn(),
  };
  const texturedPrimitiveBuffer = {
    destroy: vi.fn(),
  };
  const waveformBuffer = {
    destroy: vi.fn(),
  };
  const vertexBuffers = [filledPrimitiveBuffer, texturedPrimitiveBuffer, waveformBuffer];
  const texture = {
    createView: vi.fn(() => 'feedback-view'),
    destroy: vi.fn(),
  };
  const device = {
    createBindGroup: vi.fn(() => ({})),
    createBindGroupLayout: vi.fn(() => ({})),
    createBuffer: vi.fn(({ usage }) =>
      (usage === 40 ? vertexBuffers.shift() || waveformBuffer : uniformBuffer)),
    createCommandEncoder: vi.fn(() => encoder),
    createPipelineLayout: vi.fn(() => ({})),
    createRenderPipeline: vi.fn(() => ({
      getBindGroupLayout: vi.fn(() => ({})),
    })),
    createSampler: vi.fn(() => ({})),
    createShaderModule: vi.fn(() => ({})),
    createTexture: vi.fn(() => texture),
    destroy: vi.fn(),
    queue: {
      copyExternalImageToTexture: vi.fn(),
      submit: vi.fn(),
      writeBuffer: vi.fn(),
      writeTexture: vi.fn(),
    },
  };
  const adapter = {
    features: new Set(['texture-compression-bc']),
    limits: {
      maxTextureArrayLayers: 256,
      maxTextureDimension2D: 8192,
    },
    requestAdapterInfo: vi.fn(() => ({
      architecture: 'discrete',
      device: 'Test GPU',
      vendor: 'Test Vendor',
    })),
    requestDevice: vi.fn(() => device),
  };
  const gpu = {
    getPreferredCanvasFormat: vi.fn(() => 'bgra8unorm'),
    requestAdapter: vi.fn(() => adapter),
  };
  const context = {
    configure: vi.fn(),
    getCurrentTexture: vi.fn(() => ({
      createView: vi.fn(() => 'texture-view'),
    })),
  };
  const canvas = {
    getContext: vi.fn((name) => (name === 'webgpu' ? context : null)),
  };

  return {
    adapter,
    canvas,
    context,
    device,
    encoder,
    filledPrimitiveBuffer,
    gpu,
    pass,
    texture,
    texturedPrimitiveBuffer,
    uniformBuffer,
    waveformBuffer,
  };
};

describe('native MilkDrop WebGPU renderer', () => {
  it('colors triangle-list vertices for WebGPU filled primitive draws', () => {
    const vertices = Array.from(createWebGpuTriangleListVertices(
      new Float32Array([-1, -1, 1, -1, 0, 1]),
      [0.2, 0.4, 0.6, 0.8],
    )).map((value) => Number(value.toFixed(3)));

    expect(vertices).toEqual([
      -1, -1, 0.2, 0.4, 0.6, 0.8,
      1, -1, 0.2, 0.4, 0.6, 0.8,
      0, 1, 0.2, 0.4, 0.6, 0.8,
    ]);
  });

  it('converts triangle-fan vertices into WebGPU triangle-list vertices', () => {
    const vertices = Array.from(createWebGpuTriangleFanVertices(
      new Float32Array([0, 0, -1, -1, 1, -1, 1, 1]),
      new Float32Array([
        1, 0, 0, 0.5,
        0, 1, 0, 0.6,
        0, 0, 1, 0.7,
        1, 1, 1, 0.8,
      ]),
    )).map((value) => Number(value.toFixed(3)));

    expect(vertices).toEqual([
      0, 0, 1, 0, 0, 0.5,
      -1, -1, 0, 1, 0, 0.6,
      1, -1, 0, 0, 1, 0.7,
      0, 0, 1, 0, 0, 0.5,
      1, -1, 0, 0, 1, 0.7,
      1, 1, 1, 1, 1, 0.8,
    ]);
  });

  it('converts textured triangle-fan vertices with uv and color attributes', () => {
    const vertices = Array.from(createWebGpuTexturedTriangleFanVertices(
      new Float32Array([0, 0, -1, -1, 1, -1, 1, 1]),
      new Float32Array([0.5, 0.5, 0, 1, 1, 1, 1, 0]),
      new Float32Array([
        1, 0, 0, 0.5,
        0, 1, 0, 0.6,
        0, 0, 1, 0.7,
        1, 1, 1, 0.8,
      ]),
    )).map((value) => Number(value.toFixed(3)));

    expect(vertices).toEqual([
      0, 0, 0.5, 0.5, 1, 0, 0, 0.5,
      -1, -1, 0, 1, 0, 1, 0, 0.6,
      1, -1, 1, 1, 0, 0, 1, 0.7,
      0, 0, 0.5, 0.5, 1, 0, 0, 0.5,
      1, -1, 1, 1, 0, 0, 1, 0.7,
      1, 1, 1, 0, 1, 1, 1, 0.8,
    ]);
  });

  it('converts line-strip waveform points into colored line-list vertices', () => {
    const vertices = Array.from(createWebGpuLineSegmentVertices(
      new Float32Array([-1, 0, 0, 0.5, 1, 0]),
      [0.1, 0.2, 0.3, 0.4],
    )).map((value) => Number(value.toFixed(3)));
    expect(vertices).toEqual([
      -1, 0, 0.1, 0.2, 0.3, 0.4,
      0, 0.5, 0.1, 0.2, 0.3, 0.4,
      0, 0.5, 0.1, 0.2, 0.3, 0.4,
      1, 0, 0.1, 0.2, 0.3, 0.4,
    ]);
  });

  it('colors existing line-list vertices for WebGPU primitive draws', () => {
    const vertices = Array.from(createWebGpuLineListVertices(
      new Float32Array([-1, -1, 1, 1]),
      [0.2, 0.4, 0.6, 0.8],
    )).map((value) => Number(value.toFixed(3)));

    expect(vertices).toEqual([
      -1, -1, 0.2, 0.4, 0.6, 0.8,
      1, 1, 0.2, 0.4, 0.6, 0.8,
    ]);
  });

  it('converts MilkDrop motion vectors into colored WebGPU line-list vertices', () => {
    const vertices = createWebGpuMotionVectorVertices({
      mv_a: 0.75,
      mv_b: 0.3,
      mv_dx: 0.5,
      mv_dy: -0.25,
      mv_g: 0.2,
      mv_l: 0.1,
      mv_r: 0.1,
      mv_x: 2,
      mv_y: 1,
    });

    expect(vertices.length).toBe(24);
    expect(Array.from(vertices.slice(2, 6)).map((value) => Number(value.toFixed(3))))
      .toEqual([0.1, 0.2, 0.3, 0.75]);
  });

  it('converts classic MilkDrop screen borders into WebGPU triangle vertices', () => {
    const vertices = createWebGpuScreenBorderVertices({
      ib_a: 0.5,
      ib_b: 1,
      ib_g: 0.8,
      ib_r: 0.2,
      ib_size: 0.05,
      ob_a: 0.4,
      ob_b: 0.3,
      ob_g: 0.2,
      ob_r: 1,
      ob_size: 0.1,
    });

    expect(vertices.length).toBe(288);
    expect(Array.from(vertices.slice(2, 6)).map((value) => Number(value.toFixed(3))))
      .toEqual([1, 0.2, 0.3, 0.4]);
  });

  it('converts enabled MilkDrop shape fills into WebGPU triangle vertices', () => {
    const vertices = createWebGpuShapeFillVertices([
      {
        baseValues: {
          a: 0.4,
          a2: 0.2,
          b: 0.3,
          b2: 0.6,
          enabled: 1,
          g: 0.2,
          g2: 0.5,
          r: 0.1,
          r2: 0.4,
          rad: 0.25,
          sides: 3,
        },
      },
      {
        baseValues: {
          enabled: 0,
          sides: 8,
        },
      },
    ]);

    expect(vertices.length).toBe(54);
    expect(Array.from(vertices.slice(2, 6)).map((value) => Number(value.toFixed(3))))
      .toEqual([0.1, 0.2, 0.3, 0.4]);
    expect(Array.from(vertices.slice(8, 12)).map((value) => Number(value.toFixed(3))))
      .toEqual([0.4, 0.5, 0.6, 0.2]);
  });

  it('converts textured MilkDrop shape fills into WebGPU textured triangle vertices', () => {
    const vertices = createWebGpuTexturedShapeVertices({
      baseValues: {
        a: 0.4,
        b: 0.3,
        enabled: 1,
        g: 0.2,
        r: 0.1,
        rad: 0.25,
        sides: 3,
        tex_zoom: 1,
        textured: 1,
      },
    });

    expect(vertices.length).toBe(72);
    expect(Array.from(vertices.slice(0, 8)).map((value) => Number(value.toFixed(3))))
      .toEqual([0, 0, 0.5, 0.5, 0.1, 0.2, 0.3, 0.4]);
  });

  it('converts enabled MilkDrop sprites into WebGPU fallback quad triangles', () => {
    const vertices = createWebGpuSpriteVertices([
      {
        baseValues: {
          a: 0.4,
          b: 0.3,
          enabled: 1,
          g: 0.2,
          h: 0.1,
          r: 0.1,
          w: 0.2,
          x: 0.5,
          y: 0.5,
        },
      },
      {
        baseValues: {
          enabled: 0,
        },
      },
    ]);

    expect(vertices.length).toBe(36);
    expect(Array.from(vertices.slice(0, 6)).map((value) => Number(value.toFixed(3))))
      .toEqual([-0.2, -0.1, 0.1, 0.2, 0.3, 0.4]);
  });

  it('converts enabled MilkDrop sprites into WebGPU textured quad triangles', () => {
    const vertices = createWebGpuTexturedSpriteVertices({
      baseValues: {
        a: 0.4,
        b: 0.3,
        enabled: 1,
        g: 0.2,
        h: 0.1,
        r: 0.1,
        w: 0.2,
        x: 0.5,
        y: 0.5,
      },
    });

    expect(vertices.length).toBe(48);
    expect(Array.from(vertices.slice(0, 8)).map((value) => Number(value.toFixed(3))))
      .toEqual([-0.2, -0.1, 0, 1, 0.1, 0.2, 0.3, 0.4]);
  });

  it('converts enabled MilkDrop shape outlines into colored line-list vertices', () => {
    const vertices = createWebGpuShapeOutlineVertices([
      {
        baseValues: {
          border_a: 0.5,
          enabled: 1,
          rad: 0.25,
          sides: 4,
          x: 0.5,
          y: 0.5,
        },
      },
      {
        baseValues: {
          enabled: 0,
          sides: 8,
        },
      },
    ], [0.1, 0.2, 0.3]);

    expect(vertices.length).toBe(48);
    expect(Array.from(vertices.slice(2, 6)).map((value) => Number(value.toFixed(3))))
      .toEqual([0.1, 0.2, 0.3, 0.5]);
  });


  it('reports unavailable WebGPU without throwing', async () => {
    await expect(getMilkdropWebGpuStatus({ navigatorRef: {} })).resolves.toEqual({
      available: false,
      backend: 'webgpu',
      reason: 'navigator.gpu unavailable',
    });
  });

  it('reports adapter capabilities without forcing a device request by default', async () => {
    const { adapter, gpu } = createFakeWebGpu();

    const status = await getMilkdropWebGpuStatus({
      navigatorRef: { gpu },
    });

    expect(status).toEqual(expect.objectContaining({
      adapterInfo: expect.objectContaining({
        device: 'Test GPU',
        vendor: 'Test Vendor',
      }),
      available: true,
      backend: 'webgpu',
      features: ['texture-compression-bc'],
      limits: expect.objectContaining({
        maxTextureDimension2D: 8192,
      }),
    }));
    expect(adapter.requestDevice).not.toHaveBeenCalled();
  });

  it('creates an opt-in WebGPU pipeline and renders a preset-colored frame', async () => {
    const {
      canvas,
      context,
      device,
      filledPrimitiveBuffer,
      gpu,
      pass,
      texture,
      texturedPrimitiveBuffer,
      uniformBuffer,
      waveformBuffer,
    } = createFakeWebGpu();

    const renderer = await createMilkdropWebGpuRenderer({
      canvas,
      navigatorRef: { gpu },
      preset: {
        baseValues: {
          wave_a: 0.5,
          wave_b: 0.3,
          wave_g: 0.2,
          wave_r: 0.1,
          ib_a: 0.5,
          ib_size: 0.05,
          mv_a: 0.7,
          mv_l: 0.1,
          mv_x: 2,
          mv_y: 1,
          ob_a: 0.4,
          ob_size: 0.1,
          q1: 0.25,
        },
        shaders: {
          comp: 'ret = tex2D(sampler_main, uv).rgb * tex2D(album_art, uv).rgb * vec3(q1, mid_att, get_waveform(0.5));',
          warp: 'ret = tex2D(sampler_noise, uv).rgb * vec3(q1, get_fft(0.25), get_fft_hz(1000));',
        },
        shapes: [
          {
            baseValues: {
              a: 0.35,
              border_a: 0.7,
              enabled: 1,
              g2: 0.4,
              rad: 0.2,
              r2: 0.8,
              sides: 3,
              texture: 'cover.png',
            },
          },
        ],
        sprites: [
          {
            baseValues: {
              a: 0.4,
              enabled: 1,
              h: 0.1,
              image: 'sprites/logo.png',
              w: 0.2,
            },
          },
        ],
      },
      textureAssets: {
        album_art: {
          data: new Uint8Array([
            255, 0, 0, 255,
            0, 255, 0, 255,
            0, 0, 255, 255,
            255, 255, 255, 255,
          ]),
          height: 2,
          width: 2,
        },
        sampler_noise: {
          data: new Uint8Array([
            32, 32, 32, 255,
            96, 96, 96, 255,
            160, 160, 160, 255,
            224, 224, 224, 255,
          ]),
          height: 2,
          width: 2,
        },
        'sprites/logo.png': {
          data: new Uint8Array([
            255, 255, 255, 255,
            128, 128, 128, 255,
            64, 64, 64, 255,
            0, 0, 0, 255,
          ]),
          height: 2,
          width: 2,
        },
      },
    });

    renderer.render({
      audio: {
        bass_att: 1.2,
        mid_att: 0.8,
        treb_att: 0.6,
      },
      sampleRate: 48000,
      samples: [-1, 0, 1],
      spectrum: [0, 128, 255],
      time: 2.5,
    }, {
      outputAlpha: 0.75,
    });
    renderer.dispose();

    expect(renderer.name).toBe('slskdN MilkDrop WebGPU');
    expect(context.configure).toHaveBeenCalledWith(expect.objectContaining({
      device,
      format: 'bgra8unorm',
    }));
    expect(device.createShaderModule).toHaveBeenCalledWith(expect.objectContaining({
      code: expect.stringContaining('@fragment'),
    }));
    expect(device.createShaderModule).toHaveBeenCalledWith(expect.objectContaining({
      code: expect.stringContaining('let q1 = uniforms.q1;'),
    }));
    expect(device.createShaderModule).toHaveBeenCalledWith(expect.objectContaining({
      code: expect.stringContaining('@group(0) @binding(3) var shaderTexture0: texture_2d<f32>;'),
    }));
    expect(device.createTexture).toHaveBeenCalledTimes(6);
    expect(device.createBuffer).toHaveBeenCalledWith(expect.objectContaining({
      usage: 40,
    }));
    expect(device.createBuffer).toHaveBeenCalledWith(expect.objectContaining({
      size: 832,
      usage: 72,
    }));
    expect(device.queue.writeBuffer).toHaveBeenCalledWith(
      uniformBuffer,
      0,
      expect.any(Float32Array),
    );
    expect(device.queue.writeBuffer.mock.calls.find(([buffer]) => buffer === uniformBuffer)[2].length)
      .toBe(208);
    expect(device.queue.writeBuffer).toHaveBeenCalledWith(
      filledPrimitiveBuffer,
      0,
      expect.any(Float32Array),
    );
    expect(device.queue.writeBuffer).toHaveBeenCalledWith(
      texturedPrimitiveBuffer,
      0,
      expect.any(Float32Array),
    );
    expect(device.queue.writeBuffer).toHaveBeenCalledWith(
      waveformBuffer,
      0,
      expect.any(Float32Array),
    );
    expect(device.queue.writeTexture).toHaveBeenCalledTimes(4);
    expect(pass.draw).toHaveBeenCalledTimes(6);
    expect(pass.draw).toHaveBeenCalledWith(3);
    expect(pass.setVertexBuffer).toHaveBeenCalledWith(0, filledPrimitiveBuffer);
    expect(pass.setVertexBuffer).toHaveBeenCalledWith(0, texturedPrimitiveBuffer);
    expect(pass.setVertexBuffer).toHaveBeenCalledWith(0, waveformBuffer);
    expect(device.queue.submit).toHaveBeenCalledWith(['command-buffer']);
    expect(texture.destroy).toHaveBeenCalledTimes(4);
    expect(filledPrimitiveBuffer.destroy).toHaveBeenCalled();
    expect(texturedPrimitiveBuffer.destroy).toHaveBeenCalled();
    expect(waveformBuffer.destroy).toHaveBeenCalled();
    expect(uniformBuffer.destroy).toHaveBeenCalled();
    expect(device.destroy).toHaveBeenCalled();
  });
});
