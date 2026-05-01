import {
  createTranslatedMilkdropWgslShader,
  getMilkdropShaderTextureSamplers,
} from './shaderTranslator';
import {
  createMotionVectorVertices,
  createScreenBorderVertices,
  createShaderFftBins,
  createShaderWaveformBins,
  createShapeFillColors,
  createShapeFillVertices,
  createShapeTextureUvs,
  createShapeVertices,
  createSpriteTextureUvs,
  createSpriteVertices,
  createWaveformVertices,
  getShapeTexture,
  getTextureNameAliases,
  getMotionVectorColor,
  getScreenBorderColor,
  getShapeBorderColor,
  getSpriteFillColor,
  isShapeTextured,
} from './milkdropRenderer';

const bufferUsageCopyDst = 0x0008;
const bufferUsageUniform = 0x0040;
const bufferUsageVertex = 0x0020;
const shaderStageFragment = 0x2;
const textureUsageCopyDst = 0x02;
const textureUsageTextureBinding = 0x04;
const textureUsageRenderAttachment = 0x10;

const getGpu = (navigatorRef) =>
  navigatorRef?.gpu
  || (typeof navigator !== 'undefined' ? navigator.gpu : null);

const getPresetNumber = (preset, key, fallback) => {
  const value = Number(preset?.baseValues?.[key]);
  return Number.isFinite(value) ? value : fallback;
};

const getPresetColor = (preset) => [
  getPresetNumber(preset, 'wave_r', 0.7),
  getPresetNumber(preset, 'wave_g', 0.7),
  getPresetNumber(preset, 'wave_b', 0.7),
  getPresetNumber(preset, 'wave_a', 1),
];

const clamp01 = (value) =>
  Math.max(0, Math.min(1, Number(value) || 0));

const qRegisterNames = Array.from({ length: 64 }, (_unused, index) => `q${index + 1}`);
const webGpuUniformFloatCount = 208;
const webGpuUniformQOffset = 16;
const webGpuUniformFftOffset = 80;
const webGpuUniformWaveformOffset = 144;
const webGpuShaderTextureLimit = 4;

export const createWebGpuTriangleListVertices = (
  triangleVertices = [],
  color = [1, 1, 1, 1],
) => {
  const vertexCount = Math.floor(triangleVertices.length / 2);
  if (vertexCount < 3) return new Float32Array();

  const vertices = new Float32Array(vertexCount * 6);
  for (let index = 0; index < vertexCount; index += 1) {
    vertices.set([
      triangleVertices[index * 2],
      triangleVertices[index * 2 + 1],
      color[0],
      color[1],
      color[2],
      color[3],
    ], index * 6);
  }
  return vertices;
};

export const createWebGpuTriangleFanVertices = (
  fanVertices = [],
  fanColors = [],
  fallbackColor = [1, 1, 1, 1],
) => {
  const vertexCount = Math.floor(fanVertices.length / 2);
  if (vertexCount < 3) return new Float32Array();

  const triangleCount = vertexCount - 2;
  const vertices = new Float32Array(triangleCount * 3 * 6);
  let offset = 0;
  const appendVertex = (vertexIndex) => {
    const colorOffset = vertexIndex * 4;
    vertices.set([
      fanVertices[vertexIndex * 2],
      fanVertices[vertexIndex * 2 + 1],
      fanColors[colorOffset] ?? fallbackColor[0],
      fanColors[colorOffset + 1] ?? fallbackColor[1],
      fanColors[colorOffset + 2] ?? fallbackColor[2],
      fanColors[colorOffset + 3] ?? fallbackColor[3],
    ], offset);
    offset += 6;
  };

  for (let index = 1; index < vertexCount - 1; index += 1) {
    appendVertex(0);
    appendVertex(index);
    appendVertex(index + 1);
  }
  return vertices;
};

export const createWebGpuTexturedTriangleFanVertices = (
  fanVertices = [],
  fanUvs = [],
  fanColors = [],
  fallbackColor = [1, 1, 1, 1],
) => {
  const vertexCount = Math.floor(fanVertices.length / 2);
  if (vertexCount < 3) return new Float32Array();

  const triangleCount = vertexCount - 2;
  const vertices = new Float32Array(triangleCount * 3 * 8);
  let offset = 0;
  const appendVertex = (vertexIndex) => {
    const colorOffset = vertexIndex * 4;
    vertices.set([
      fanVertices[vertexIndex * 2],
      fanVertices[vertexIndex * 2 + 1],
      fanUvs[vertexIndex * 2] ?? 0.5,
      fanUvs[vertexIndex * 2 + 1] ?? 0.5,
      fanColors[colorOffset] ?? fallbackColor[0],
      fanColors[colorOffset + 1] ?? fallbackColor[1],
      fanColors[colorOffset + 2] ?? fallbackColor[2],
      fanColors[colorOffset + 3] ?? fallbackColor[3],
    ], offset);
    offset += 8;
  };

  for (let index = 1; index < vertexCount - 1; index += 1) {
    appendVertex(0);
    appendVertex(index);
    appendVertex(index + 1);
  }
  return vertices;
};

export const createWebGpuLineSegmentVertices = (lineStripVertices = [], color = [1, 1, 1, 1]) => {
  const vertexCount = Math.floor(lineStripVertices.length / 2);
  if (vertexCount < 2) return new Float32Array();

  const segmentVertices = new Float32Array((vertexCount - 1) * 2 * 6);
  let offset = 0;
  for (let index = 0; index < vertexCount - 1; index += 1) {
    const pointOffset = index * 2;
    const nextPointOffset = pointOffset + 2;
    segmentVertices.set([
      lineStripVertices[pointOffset],
      lineStripVertices[pointOffset + 1],
      color[0],
      color[1],
      color[2],
      color[3],
      lineStripVertices[nextPointOffset],
      lineStripVertices[nextPointOffset + 1],
      color[0],
      color[1],
      color[2],
      color[3],
    ], offset);
    offset += 12;
  }

  return segmentVertices;
};

export const createWebGpuLineListVertices = (lineListVertices = [], color = [1, 1, 1, 1]) => {
  const vertexCount = Math.floor(lineListVertices.length / 2);
  if (vertexCount < 2) return new Float32Array();

  const vertices = new Float32Array(vertexCount * 6);
  for (let index = 0; index < vertexCount; index += 1) {
    vertices.set([
      lineListVertices[index * 2],
      lineListVertices[index * 2 + 1],
      color[0],
      color[1],
      color[2],
      color[3],
    ], index * 6);
  }
  return vertices;
};

const concatFloat32Arrays = (arrays) => {
  const totalLength = arrays.reduce((total, array) => total + array.length, 0);
  const result = new Float32Array(totalLength);
  let offset = 0;
  arrays.forEach((array) => {
    result.set(array, offset);
    offset += array.length;
  });
  return result;
};

export const createWebGpuShapeOutlineVertices = (shapes = [], fallbackColor = [0.7, 0.7, 0.7]) =>
  concatFloat32Arrays(
    shapes
      .map((shape) => createWebGpuLineSegmentVertices(
        createShapeVertices(shape),
        getShapeBorderColor(shape, fallbackColor),
      ))
      .filter((vertices) => vertices.length > 0),
  );

export const createWebGpuShapeFillVertices = (shapes = [], fallbackColor = [0.7, 0.7, 0.7]) =>
  concatFloat32Arrays(
    shapes
      .filter((shape) => !isShapeTextured(shape))
      .map((shape) => createWebGpuTriangleFanVertices(
        createShapeFillVertices(shape),
        createShapeFillColors(shape, fallbackColor),
        [...fallbackColor, 0.6],
      ))
      .filter((vertices) => vertices.length > 0),
  );

export const createWebGpuTexturedShapeVertices = (shape = {}, fallbackColor = [0.7, 0.7, 0.7]) =>
  createWebGpuTexturedTriangleFanVertices(
    createShapeFillVertices(shape),
    createShapeTextureUvs(shape),
    createShapeFillColors(shape, fallbackColor),
    [...fallbackColor, 0.6],
  );

const createWebGpuTexturedQuadVertices = (
  quadVertices = [],
  quadUvs = [],
  color = [1, 1, 1, 1],
) => {
  if (quadVertices.length < 8 || quadUvs.length < 8) return new Float32Array();
  const triangleVertexIndexes = [0, 1, 2, 0, 2, 3];
  const vertices = new Float32Array(triangleVertexIndexes.length * 8);
  let offset = 0;
  triangleVertexIndexes.forEach((vertexIndex) => {
    vertices.set([
      quadVertices[vertexIndex * 2],
      quadVertices[vertexIndex * 2 + 1],
      quadUvs[vertexIndex * 2],
      quadUvs[vertexIndex * 2 + 1],
      color[0],
      color[1],
      color[2],
      color[3],
    ], offset);
    offset += 8;
  });
  return vertices;
};

export const createWebGpuSpriteVertices = (sprites = [], fallbackColor = [1, 1, 1]) =>
  concatFloat32Arrays(
    sprites
      .map((sprite) => {
        const spriteVertices = createSpriteVertices(sprite);
        if (spriteVertices.length < 8) return new Float32Array();
        const color = getSpriteFillColor(sprite, fallbackColor);
        return createWebGpuTriangleListVertices(
          new Float32Array([
            spriteVertices[0], spriteVertices[1],
            spriteVertices[2], spriteVertices[3],
            spriteVertices[4], spriteVertices[5],
            spriteVertices[0], spriteVertices[1],
            spriteVertices[4], spriteVertices[5],
            spriteVertices[6], spriteVertices[7],
          ]),
          color,
        );
      })
      .filter((vertices) => vertices.length > 0),
  );

export const createWebGpuTexturedSpriteVertices = (sprite = {}, fallbackColor = [1, 1, 1]) =>
  createWebGpuTexturedQuadVertices(
    createSpriteVertices(sprite),
    createSpriteTextureUvs(sprite),
    getSpriteFillColor(sprite, fallbackColor),
  );

export const createWebGpuMotionVectorVertices = (scope = {}, fallbackColor = [0.7, 0.7, 0.7]) =>
  createWebGpuLineListVertices(
    createMotionVectorVertices(scope),
    getMotionVectorColor(scope, fallbackColor),
  );

export const createWebGpuScreenBorderVertices = (scope = {}, fallbackColor = [0.7, 0.7, 0.7]) =>
  concatFloat32Arrays([
    createWebGpuTriangleListVertices(
      createScreenBorderVertices(scope.ob_size),
      getScreenBorderColor(scope, 'ob', fallbackColor),
    ),
    createWebGpuTriangleListVertices(
      createScreenBorderVertices(scope.ib_size, clamp01(scope.ob_size) * 2),
      getScreenBorderColor(scope, 'ib', fallbackColor),
    ),
  ].filter((vertices) => vertices.length > 0));

const formatAdapterInfo = (info = {}) => ({
  architecture: info.architecture || '',
  description: info.description || '',
  device: info.device || '',
  vendor: info.vendor || '',
});

const createWebGpuTextureFromPixels = (device, width, height, data) => {
  const safeWidth = Math.max(1, width);
  const safeHeight = Math.max(1, height);
  const sourceBytesPerRow = safeWidth * 4;
  const bytesPerRow = Math.ceil(sourceBytesPerRow / 256) * 256;
  const textureData = bytesPerRow === sourceBytesPerRow
    ? data
    : new Uint8Array(bytesPerRow * safeHeight);
  if (textureData !== data) {
    for (let row = 0; row < safeHeight; row += 1) {
      textureData.set(
        data.subarray(row * sourceBytesPerRow, (row + 1) * sourceBytesPerRow),
        row * bytesPerRow,
      );
    }
  }
  const texture = device.createTexture({
    format: 'rgba8unorm',
    size: [safeWidth, safeHeight],
    usage: textureUsageTextureBinding | textureUsageCopyDst,
  });
  device.queue.writeTexture(
    { texture },
    textureData,
    {
      bytesPerRow,
      rowsPerImage: safeHeight,
    },
    [safeWidth, safeHeight],
  );
  return texture;
};

const createWebGpuProceduralTexture = (device) =>
  createWebGpuTextureFromPixels(
    device,
    2,
    2,
    new Uint8Array([
      255, 255, 255, 255,
      96, 199, 217, 255,
      139, 212, 80, 255,
      242, 184, 75, 255,
    ]),
  );

const createWebGpuTextureFromDataUrlAsset = async (device, asset) => {
  if (
    typeof fetch !== 'function'
    || typeof createImageBitmap !== 'function'
    || typeof device.queue.copyExternalImageToTexture !== 'function'
    || !asset?.dataUrl
  ) {
    return null;
  }
  const response = await fetch(asset.dataUrl);
  const image = await createImageBitmap(await response.blob());
  const texture = device.createTexture({
    format: 'rgba8unorm',
    size: [Math.max(1, image.width), Math.max(1, image.height)],
    usage: textureUsageTextureBinding | textureUsageCopyDst,
  });
  device.queue.copyExternalImageToTexture(
    { source: image },
    { texture },
    [Math.max(1, image.width), Math.max(1, image.height)],
  );
  image.close?.();
  return texture;
};

const createWebGpuNamedTextures = async (device, textureAssets = {}) => {
  const textures = {};
  const uniqueTextures = new Set();
  for (const [rawName, asset] of Object.entries(textureAssets)) {
    const aliases = getTextureNameAliases(rawName);
    if (!aliases.length || !asset) continue;
    const texture = asset.data
      ? createWebGpuTextureFromPixels(
        device,
        Math.max(1, Number(asset.width) || 1),
        Math.max(1, Number(asset.height) || 1),
        asset.data,
      )
      : await createWebGpuTextureFromDataUrlAsset(device, asset);
    if (!texture) continue;
    uniqueTextures.add(texture);
    aliases.forEach((name) => {
      textures[name] = texture;
    });
  }
  return {
    textures,
    uniqueTextures,
  };
};

export const getMilkdropWebGpuStatus = async ({ navigatorRef, requestDevice = false } = {}) => {
  const gpu = getGpu(navigatorRef);
  if (!gpu) {
    return {
      available: false,
      backend: 'webgpu',
      reason: 'navigator.gpu unavailable',
    };
  }

  let adapter;
  try {
    adapter = await gpu.requestAdapter();
  } catch (error) {
    return {
      available: false,
      backend: 'webgpu',
      reason: error?.message || 'WebGPU adapter request failed',
    };
  }

  if (!adapter) {
    return {
      available: false,
      backend: 'webgpu',
      reason: 'WebGPU adapter unavailable',
    };
  }

  const adapterInfo = adapter.requestAdapterInfo
    ? formatAdapterInfo(await adapter.requestAdapterInfo())
    : formatAdapterInfo();

  if (requestDevice) {
    let device;
    try {
      device = await adapter.requestDevice();
    } catch (error) {
      return {
        adapterInfo,
        available: false,
        backend: 'webgpu',
        features: Array.from(adapter.features || []),
        reason: error?.message || 'WebGPU device request failed',
      };
    } finally {
      device?.destroy?.();
    }
  }

  return {
    adapterInfo,
    available: true,
    backend: 'webgpu',
    features: Array.from(adapter.features || []),
    limits: {
      maxTextureDimension2D: adapter.limits?.maxTextureDimension2D,
      maxTextureArrayLayers: adapter.limits?.maxTextureArrayLayers,
    },
  };
};

const webGpuShaderSource = `
struct Uniforms {
  color: vec4f,
  time: f32,
  bass: f32,
  mid: f32,
  treb: f32,
  feedback: f32,
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;
@group(0) @binding(1) var previousFrame: texture_2d<f32>;
@group(0) @binding(2) var previousSampler: sampler;

struct VertexOutput {
  @builtin(position) position: vec4f,
  @location(0) uv: vec2f,
};

@vertex
fn vertexMain(@builtin(vertex_index) vertexIndex: u32) -> VertexOutput {
  var positions = array<vec2f, 3>(
    vec2f(-1.0, -1.0),
    vec2f(3.0, -1.0),
    vec2f(-1.0, 3.0)
  );
  var output: VertexOutput;
  output.position = vec4f(positions[vertexIndex], 0.0, 1.0);
  output.uv = positions[vertexIndex] * 0.5 + vec2f(0.5);
  return output;
}

@fragment
fn fragmentMain(input: VertexOutput) -> @location(0) vec4f {
  let pulse = 0.75 + 0.25 * sin(uniforms.time + input.uv.x * 6.2831853);
  let audioTint = vec3f(
    max(uniforms.bass, 0.05),
    max(uniforms.mid, 0.05),
    max(uniforms.treb, 0.05)
  );
  let previous = textureSample(previousFrame, previousSampler, clamp(input.uv, vec2f(0.0), vec2f(1.0))).rgb;
  let current = uniforms.color.rgb * pulse * audioTint;
  return vec4f(mix(current, previous, uniforms.feedback), uniforms.color.a);
}
`;

const webGpuDisplayShaderSource = `
@group(0) @binding(1) var previousFrame: texture_2d<f32>;
@group(0) @binding(2) var previousSampler: sampler;

struct VertexOutput {
  @builtin(position) position: vec4f,
  @location(0) uv: vec2f,
};

@vertex
fn vertexMain(@builtin(vertex_index) vertexIndex: u32) -> VertexOutput {
  var positions = array<vec2f, 3>(
    vec2f(-1.0, -1.0),
    vec2f(3.0, -1.0),
    vec2f(-1.0, 3.0)
  );
  var output: VertexOutput;
  output.position = vec4f(positions[vertexIndex], 0.0, 1.0);
  output.uv = positions[vertexIndex] * 0.5 + vec2f(0.5);
  return output;
}

@fragment
fn fragmentMain(input: VertexOutput) -> @location(0) vec4f {
  return textureSample(previousFrame, previousSampler, clamp(input.uv, vec2f(0.0), vec2f(1.0)));
}
`;

const webGpuWaveformShaderSource = `
struct VertexOutput {
  @builtin(position) position: vec4f,
  @location(0) color: vec4f,
};

@vertex
fn vertexMain(
  @location(0) position: vec2f,
  @location(1) color: vec4f
) -> VertexOutput {
  var output: VertexOutput;
  output.position = vec4f(position, 0.0, 1.0);
  output.color = color;
  return output;
}

@fragment
fn fragmentMain(input: VertexOutput) -> @location(0) vec4f {
  return input.color;
}
`;

const webGpuTexturedPrimitiveShaderSource = `
@group(0) @binding(0) var primitiveTexture: texture_2d<f32>;
@group(0) @binding(1) var primitiveSampler: sampler;

struct VertexOutput {
  @builtin(position) position: vec4f,
  @location(0) uv: vec2f,
  @location(1) color: vec4f,
};

@vertex
fn vertexMain(
  @location(0) position: vec2f,
  @location(1) sourceUv: vec2f,
  @location(2) color: vec4f
) -> VertexOutput {
  var output: VertexOutput;
  output.position = vec4f(position, 0.0, 1.0);
  output.uv = sourceUv;
  output.color = color;
  return output;
}

@fragment
fn fragmentMain(input: VertexOutput) -> @location(0) vec4f {
  let texel = textureSample(primitiveTexture, primitiveSampler, fract(input.uv));
  return vec4f(texel.rgb * input.color.rgb, texel.a * input.color.a);
}
`;

export const createMilkdropWebGpuRenderer = async ({
  canvas,
  navigatorRef,
  preset,
  textureAssets = {},
}) => {
  const gpu = getGpu(navigatorRef);
  if (!gpu) throw new Error('WebGPU is unavailable for the native MilkDrop renderer.');

  const adapter = await gpu.requestAdapter();
  if (!adapter) throw new Error('WebGPU adapter is unavailable for the native MilkDrop renderer.');

  const device = await adapter.requestDevice();
  const context = canvas.getContext('webgpu');
  if (!context) {
    device.destroy?.();
    throw new Error('WebGPU canvas context is unavailable for the native MilkDrop renderer.');
  }

  const format = gpu.getPreferredCanvasFormat?.() || 'bgra8unorm';
  context.configure({
    alphaMode: 'premultiplied',
    device,
    format,
  });

  const uniformBuffer = device.createBuffer({
    size: webGpuUniformFloatCount * 4,
    usage: bufferUsageUniform | bufferUsageCopyDst,
  });
  const sampler = device.createSampler({
    magFilter: 'linear',
    minFilter: 'linear',
  });
  const translatedWarpShaderSource = createTranslatedMilkdropWgslShader(preset.shaders?.warp);
  const translatedCompShaderSource = createTranslatedMilkdropWgslShader(preset.shaders?.comp);
  const translatedWarpTextureSamplers = translatedWarpShaderSource
    ? getMilkdropShaderTextureSamplers(preset.shaders?.warp)
    : [];
  const translatedCompTextureSamplers = translatedCompShaderSource
    ? getMilkdropShaderTextureSamplers(preset.shaders?.comp)
    : [];
  const shaderModule = device.createShaderModule({
    code: translatedWarpShaderSource || webGpuShaderSource,
  });
  const bindGroupLayout = device.createBindGroupLayout({
    entries: [
      {
        binding: 0,
        buffer: { type: 'uniform' },
        visibility: shaderStageFragment,
      },
      {
        binding: 1,
        texture: { sampleType: 'float' },
        visibility: shaderStageFragment,
      },
      {
        binding: 2,
        sampler: { type: 'filtering' },
        visibility: shaderStageFragment,
      },
      ...Array.from({ length: webGpuShaderTextureLimit }, (_unused, index) => ({
        binding: index + 3,
        texture: { sampleType: 'float' },
        visibility: shaderStageFragment,
      })),
      {
        binding: 7,
        sampler: { type: 'filtering' },
        visibility: shaderStageFragment,
      },
    ],
  });
  const pipelineLayout = device.createPipelineLayout({
    bindGroupLayouts: [bindGroupLayout],
  });
  const pipeline = device.createRenderPipeline({
    fragment: {
      entryPoint: 'fragmentMain',
      module: shaderModule,
      targets: [{ format }],
    },
    layout: pipelineLayout,
    primitive: {
      topology: 'triangle-list',
    },
    vertex: {
      entryPoint: 'vertexMain',
      module: shaderModule,
    },
  });
  const displayShaderModule = device.createShaderModule({
    code: translatedCompShaderSource || webGpuDisplayShaderSource,
  });
  const displayPipeline = device.createRenderPipeline({
    fragment: {
      entryPoint: 'fragmentMain',
      module: displayShaderModule,
      targets: [{ format }],
    },
    layout: pipelineLayout,
    primitive: {
      topology: 'triangle-list',
    },
    vertex: {
      entryPoint: 'vertexMain',
      module: displayShaderModule,
    },
  });
  const waveformShaderModule = device.createShaderModule({ code: webGpuWaveformShaderSource });
  const createPrimitivePipeline = (topology) => device.createRenderPipeline({
    fragment: {
      entryPoint: 'fragmentMain',
      module: waveformShaderModule,
      targets: [
        {
          blend: {
            alpha: {
              dstFactor: 'one-minus-src-alpha',
              operation: 'add',
              srcFactor: 'src-alpha',
            },
            color: {
              dstFactor: 'one-minus-src-alpha',
              operation: 'add',
              srcFactor: 'src-alpha',
            },
          },
          format,
        },
      ],
    },
    layout: 'auto',
    primitive: {
      topology,
    },
    vertex: {
      buffers: [
        {
          arrayStride: 24,
          attributes: [
            {
              format: 'float32x2',
              offset: 0,
              shaderLocation: 0,
            },
            {
              format: 'float32x4',
              offset: 8,
              shaderLocation: 1,
            },
          ],
        },
      ],
      entryPoint: 'vertexMain',
      module: waveformShaderModule,
    },
  });
  const filledPrimitivePipeline = createPrimitivePipeline('triangle-list');
  const linePrimitivePipeline = createPrimitivePipeline('line-list');
  const texturedPrimitiveShaderModule = device.createShaderModule({
    code: webGpuTexturedPrimitiveShaderSource,
  });
  const texturedPrimitivePipeline = device.createRenderPipeline({
    fragment: {
      entryPoint: 'fragmentMain',
      module: texturedPrimitiveShaderModule,
      targets: [
        {
          blend: {
            alpha: {
              dstFactor: 'one-minus-src-alpha',
              operation: 'add',
              srcFactor: 'src-alpha',
            },
            color: {
              dstFactor: 'one-minus-src-alpha',
              operation: 'add',
              srcFactor: 'src-alpha',
            },
          },
          format,
        },
      ],
    },
    layout: 'auto',
    primitive: {
      topology: 'triangle-list',
    },
    vertex: {
      buffers: [
        {
          arrayStride: 32,
          attributes: [
            {
              format: 'float32x2',
              offset: 0,
              shaderLocation: 0,
            },
            {
              format: 'float32x2',
              offset: 8,
              shaderLocation: 1,
            },
            {
              format: 'float32x4',
              offset: 16,
              shaderLocation: 2,
            },
          ],
        },
      ],
      entryPoint: 'vertexMain',
      module: texturedPrimitiveShaderModule,
    },
  });
  const proceduralPrimitiveTexture = createWebGpuProceduralTexture(device);
  const {
    textures: namedPrimitiveTextures,
    uniqueTextures: namedPrimitiveTextureSet,
  } = await createWebGpuNamedTextures(device, textureAssets);
  const primitiveTextureBindGroups = new Map();
  let feedbackSize = { height: 0, width: 0 };
  let feedbackTextures = [];
  let warpFeedbackBindGroups = [];
  let displayFeedbackBindGroups = [];
  let readFeedbackIndex = 0;
  let readFeedbackInitialized = false;
  let filledPrimitiveBuffer = null;
  let filledPrimitiveBufferSize = 0;
  let texturedPrimitiveBuffer = null;
  let texturedPrimitiveBufferSize = 0;
  let waveformBuffer = null;
  let waveformBufferSize = 0;

  const disposeFilledPrimitiveBuffer = () => {
    filledPrimitiveBuffer?.destroy?.();
    filledPrimitiveBuffer = null;
    filledPrimitiveBufferSize = 0;
  };

  const disposeWaveformBuffer = () => {
    waveformBuffer?.destroy?.();
    waveformBuffer = null;
    waveformBufferSize = 0;
  };

  const disposeTexturedPrimitiveBuffer = () => {
    texturedPrimitiveBuffer?.destroy?.();
    texturedPrimitiveBuffer = null;
    texturedPrimitiveBufferSize = 0;
  };

  const ensureFilledPrimitiveBuffer = (byteLength) => {
    if (byteLength <= 0) return null;
    if (filledPrimitiveBuffer && filledPrimitiveBufferSize >= byteLength) {
      return filledPrimitiveBuffer;
    }
    disposeFilledPrimitiveBuffer();
    filledPrimitiveBufferSize = Math.max(byteLength, 4096);
    filledPrimitiveBuffer = device.createBuffer({
      size: filledPrimitiveBufferSize,
      usage: bufferUsageVertex | bufferUsageCopyDst,
    });
    return filledPrimitiveBuffer;
  };

  const ensureWaveformBuffer = (byteLength) => {
    if (byteLength <= 0) return null;
    if (waveformBuffer && waveformBufferSize >= byteLength) return waveformBuffer;
    disposeWaveformBuffer();
    waveformBufferSize = Math.max(byteLength, 4096);
    waveformBuffer = device.createBuffer({
      size: waveformBufferSize,
      usage: bufferUsageVertex | bufferUsageCopyDst,
    });
    return waveformBuffer;
  };

  const ensureTexturedPrimitiveBuffer = (byteLength) => {
    if (byteLength <= 0) return null;
    if (texturedPrimitiveBuffer && texturedPrimitiveBufferSize >= byteLength) {
      return texturedPrimitiveBuffer;
    }
    disposeTexturedPrimitiveBuffer();
    texturedPrimitiveBufferSize = Math.max(byteLength, 4096);
    texturedPrimitiveBuffer = device.createBuffer({
      size: texturedPrimitiveBufferSize,
      usage: bufferUsageVertex | bufferUsageCopyDst,
    });
    return texturedPrimitiveBuffer;
  };

  const disposeFeedbackTextures = () => {
    feedbackTextures.forEach((texture) => texture.destroy?.());
    feedbackTextures = [];
    warpFeedbackBindGroups = [];
    displayFeedbackBindGroups = [];
  };

  const getPrimitiveTextureBindGroup = (texture) => {
    if (primitiveTextureBindGroups.has(texture)) {
      return primitiveTextureBindGroups.get(texture);
    }
    const bindGroup = device.createBindGroup({
      entries: [
        {
          binding: 0,
          resource: texture.createView(),
        },
        {
          binding: 1,
          resource: sampler,
        },
      ],
      layout: texturedPrimitivePipeline.getBindGroupLayout(0),
    });
    primitiveTextureBindGroups.set(texture, bindGroup);
    return bindGroup;
  };

  const resolvePrimitiveTexture = (entry) =>
    getShapeTexture(entry, namedPrimitiveTextures, proceduralPrimitiveTexture);

  const resolveShaderTexture = (samplerName) =>
    getTextureNameAliases(samplerName)
      .map((alias) => namedPrimitiveTextures[alias])
      .find(Boolean) || proceduralPrimitiveTexture;

  const createFeedbackBindGroup = (texture, textureSamplers = []) =>
    device.createBindGroup({
      entries: [
        {
          binding: 0,
          resource: {
            buffer: uniformBuffer,
          },
        },
        {
          binding: 1,
          resource: texture.createView(),
        },
        {
          binding: 2,
          resource: sampler,
        },
        ...Array.from({ length: webGpuShaderTextureLimit }, (_unused, index) => ({
          binding: index + 3,
          resource: resolveShaderTexture(textureSamplers[index]).createView(),
        })),
        {
          binding: 7,
          resource: sampler,
        },
      ],
      layout: bindGroupLayout,
    });

  const ensureFeedbackTextures = () => {
    const width = Math.max(1, Number(canvas.width) || 1);
    const height = Math.max(1, Number(canvas.height) || 1);
    if (
      feedbackTextures.length === 2
      && feedbackSize.width === width
      && feedbackSize.height === height
    ) {
      return;
    }

    disposeFeedbackTextures();
    feedbackSize = { height, width };
    feedbackTextures = [0, 1].map(() =>
      device.createTexture({
        format,
        size: [width, height],
        usage: 20,
      }));
    warpFeedbackBindGroups = feedbackTextures.map((texture) =>
      createFeedbackBindGroup(texture, translatedWarpTextureSamplers));
    displayFeedbackBindGroups = feedbackTextures.map((texture) =>
      createFeedbackBindGroup(texture, translatedCompTextureSamplers));
    readFeedbackIndex = 0;
    readFeedbackInitialized = false;
  };

  return {
    backend: 'webgpu',
    name: 'slskdN MilkDrop WebGPU',
    dispose: () => {
      disposeFeedbackTextures();
      disposeFilledPrimitiveBuffer();
      disposeTexturedPrimitiveBuffer();
      disposeWaveformBuffer();
      proceduralPrimitiveTexture.destroy?.();
      namedPrimitiveTextureSet.forEach((texture) => texture.destroy?.());
      uniformBuffer.destroy?.();
      device.destroy?.();
    },
    render: (frame = {}, options = {}) => {
      ensureFeedbackTextures();
      const writeFeedbackIndex = readFeedbackIndex === 0 ? 1 : 0;
      const color = getPresetColor(preset);
      const audio = frame.audio || {};
      const uniforms = new Float32Array(webGpuUniformFloatCount);
      uniforms.set([
        color[0],
        color[1],
        color[2],
        Math.max(0, Math.min(1, color[3] * (options.outputAlpha ?? 1))),
        Number(frame.time) || 0,
        Number(audio.bass ?? audio.bass_att ?? 1) || 1,
        Number(audio.mid ?? audio.mid_att ?? 1) || 1,
        Number(audio.treb ?? audio.treb_att ?? 1) || 1,
        Number(audio.bass_att ?? audio.bass ?? 1) || 1,
        Number(audio.mid_att ?? audio.mid ?? 1) || 1,
        Number(audio.treb_att ?? audio.treb ?? 1) || 1,
        readFeedbackInitialized
          ? Math.max(0, Math.min(1, getPresetNumber(preset, 'decay', 0.9)))
          : 0,
        Math.max(0, Math.min(1, options.outputAlpha ?? 1)),
        Number(frame.sampleRate ?? frame.sample_rate ?? 44100) || 44100,
        Math.max(1, Number(canvas.width) || 1),
        Math.max(1, Number(canvas.height) || 1),
      ], 0);
      qRegisterNames.forEach((key, index) => {
        uniforms[webGpuUniformQOffset + index] = Number(preset.baseValues?.[key] ?? 0) || 0;
      });
      uniforms.set(
        createShaderFftBins(frame.spectrum || frame.frequencies || frame.frequency || frame.fft || []),
        webGpuUniformFftOffset,
      );
      uniforms.set(
        createShaderWaveformBins(frame.waveform || frame.samples || []),
        webGpuUniformWaveformOffset,
      );
      const waveformLineStrip = createWaveformVertices(
        frame.waveform || frame.samples || [],
        {
          mode: getPresetNumber(preset, 'wave_mode', 0),
          scale: getPresetNumber(preset, 'wave_scale', 1),
          smoothing: getPresetNumber(preset, 'wave_smoothing', 0),
          x: getPresetNumber(preset, 'wave_x', 0.5),
          y: getPresetNumber(preset, 'wave_y', 0.5),
        },
      );
      const waveformVertices = createWebGpuLineSegmentVertices(
        waveformLineStrip,
        [color[0], color[1], color[2], color[3] * (options.outputAlpha ?? 1)],
      );
      const filledPrimitiveVertices = createWebGpuScreenBorderVertices(preset.baseValues, color);
      const shapeFillVertices = createWebGpuShapeFillVertices(preset.shapes, color);
      let texturedPrimitiveVertexOffset = 0;
      const texturedPrimitiveBatches = [
        ...(preset.shapes || [])
          .filter((shape) => isShapeTextured(shape))
          .map((shape) => ({
            entry: shape,
            vertices: createWebGpuTexturedShapeVertices(shape, color),
          })),
        ...(preset.sprites || [])
          .map((sprite) => ({
            entry: sprite,
            vertices: createWebGpuTexturedSpriteVertices(sprite, color),
          })),
      ]
        .filter((batch) => batch.vertices.length > 0)
        .map((batch) => {
          const nextBatch = {
            ...batch,
            firstVertex: texturedPrimitiveVertexOffset,
            texture: resolvePrimitiveTexture(batch.entry),
          };
          texturedPrimitiveVertexOffset += batch.vertices.length / 8;
          return nextBatch;
        });
      const texturedPrimitiveVertices = concatFloat32Arrays(
        texturedPrimitiveBatches.map((batch) => batch.vertices),
      );
      const shapeVertices = createWebGpuShapeOutlineVertices(preset.shapes, color);
      const motionVectorVertices = createWebGpuMotionVectorVertices(preset.baseValues, color);
      const primitiveVertices = concatFloat32Arrays([
        waveformVertices,
        shapeVertices,
        motionVectorVertices,
      ]);
      const allFilledPrimitiveVertices = concatFloat32Arrays([
        filledPrimitiveVertices,
        shapeFillVertices,
      ]);
      device.queue.writeBuffer(uniformBuffer, 0, uniforms);
      const encoder = device.createCommandEncoder();
      const feedbackPass = encoder.beginRenderPass({
        colorAttachments: [
          {
            clearValue: { a: 0, b: 0, g: 0, r: 0 },
            loadOp: 'clear',
            storeOp: 'store',
            view: feedbackTextures[writeFeedbackIndex].createView(),
          },
        ],
      });
      feedbackPass.setPipeline(pipeline);
      feedbackPass.setBindGroup(0, warpFeedbackBindGroups[readFeedbackIndex]);
      feedbackPass.draw(3);
      feedbackPass.end();

      if (allFilledPrimitiveVertices.length > 0) {
        const filledPrimitivePass = encoder.beginRenderPass({
          colorAttachments: [
            {
              loadOp: 'load',
              storeOp: 'store',
              view: feedbackTextures[writeFeedbackIndex].createView(),
            },
          ],
        });
        const vertexBuffer = ensureFilledPrimitiveBuffer(allFilledPrimitiveVertices.byteLength);
        device.queue.writeBuffer(vertexBuffer, 0, allFilledPrimitiveVertices);
        filledPrimitivePass.setPipeline(filledPrimitivePipeline);
        filledPrimitivePass.setVertexBuffer(0, vertexBuffer);
        filledPrimitivePass.draw(allFilledPrimitiveVertices.length / 6);
        filledPrimitivePass.end();
      }

      if (texturedPrimitiveVertices.length > 0) {
        const texturedPrimitivePass = encoder.beginRenderPass({
          colorAttachments: [
            {
              loadOp: 'load',
              storeOp: 'store',
              view: feedbackTextures[writeFeedbackIndex].createView(),
            },
          ],
        });
        const vertexBuffer = ensureTexturedPrimitiveBuffer(texturedPrimitiveVertices.byteLength);
        device.queue.writeBuffer(vertexBuffer, 0, texturedPrimitiveVertices);
        texturedPrimitivePass.setPipeline(texturedPrimitivePipeline);
        texturedPrimitivePass.setVertexBuffer(0, vertexBuffer);
        texturedPrimitiveBatches.forEach((batch) => {
          texturedPrimitivePass.setBindGroup(0, getPrimitiveTextureBindGroup(batch.texture));
          texturedPrimitivePass.draw(batch.vertices.length / 8, 1, batch.firstVertex);
        });
        texturedPrimitivePass.end();
      }

      if (primitiveVertices.length > 0) {
        const waveformPass = encoder.beginRenderPass({
          colorAttachments: [
            {
              loadOp: 'load',
              storeOp: 'store',
              view: feedbackTextures[writeFeedbackIndex].createView(),
            },
          ],
        });
        const vertexBuffer = ensureWaveformBuffer(primitiveVertices.byteLength);
        device.queue.writeBuffer(vertexBuffer, 0, primitiveVertices);
        waveformPass.setPipeline(linePrimitivePipeline);
        waveformPass.setVertexBuffer(0, vertexBuffer);
        waveformPass.draw(primitiveVertices.length / 6);
        waveformPass.end();
      }

      const displayPass = encoder.beginRenderPass({
        colorAttachments: [
          {
            clearValue: { a: 0, b: 0, g: 0, r: 0 },
            loadOp: options.clearScreen === false ? 'load' : 'clear',
            storeOp: 'store',
            view: context.getCurrentTexture().createView(),
          },
        ],
      });
      displayPass.setPipeline(displayPipeline);
      displayPass.setBindGroup(0, displayFeedbackBindGroups[writeFeedbackIndex]);
      displayPass.draw(3);
      displayPass.end();
      device.queue.submit([encoder.finish()]);
      readFeedbackIndex = writeFeedbackIndex;
      readFeedbackInitialized = true;
    },
    resize: () => {
      disposeFeedbackTextures();
      readFeedbackInitialized = false;
    },
  };
};
