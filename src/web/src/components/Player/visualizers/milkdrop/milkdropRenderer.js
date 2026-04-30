import { evaluateMilkdropEquations } from './expressionVm';
import { createTranslatedMilkdropFragmentShader } from './shaderTranslator';

const vertexShaderSource = `#version 300 es
in vec2 position;
out vec2 uv;
void main() {
  uv = position * 0.5 + 0.5;
  gl_Position = vec4(position, 0.0, 1.0);
}`;

const fragmentShaderSource = `#version 300 es
precision highp float;
uniform vec3 color;
uniform sampler2D previousFrame;
uniform float feedback;
uniform float outputAlpha;
uniform vec2 translate;
uniform float rotation;
uniform float zoom;
in vec2 uv;
out vec4 outColor;
void main() {
  vec2 centered = uv - vec2(0.5);
  float s = sin(rotation);
  float c = cos(rotation);
  mat2 rotate = mat2(c, -s, s, c);
  vec2 warped = (rotate * (centered / max(zoom, 0.001))) + vec2(0.5) + translate;
  vec3 previous = texture(previousFrame, clamp(warped, vec2(0.0), vec2(1.0))).rgb;
  outColor = vec4(mix(color, previous, feedback), outputAlpha);
}`;

const warpGridVertexShaderSource = `#version 300 es
in vec2 position;
in vec2 sourceUv;
out vec2 uv;
void main() {
  uv = sourceUv;
  gl_Position = vec4(position, 0.0, 1.0);
}`;

const warpGridFragmentShaderSource = `#version 300 es
precision highp float;
uniform vec3 color;
uniform sampler2D previousFrame;
uniform float feedback;
uniform float outputAlpha;
in vec2 uv;
out vec4 outColor;
void main() {
  vec3 previous = texture(previousFrame, clamp(uv, vec2(0.0), vec2(1.0))).rgb;
  outColor = vec4(mix(color, previous, feedback), outputAlpha);
}`;

const lineVertexShaderSource = `#version 300 es
in vec2 position;
in vec4 vertexColor;
uniform float pointSize;
out vec4 color;
void main() {
  color = vertexColor;
  gl_PointSize = pointSize;
  gl_Position = vec4(position, 0.0, 1.0);
}`;

const lineFragmentShaderSource = `#version 300 es
precision highp float;
in vec4 color;
out vec4 outColor;
void main() {
  outColor = color;
}`;

const texturedShapeVertexShaderSource = `#version 300 es
in vec2 position;
in vec2 sourceUv;
out vec2 uv;
void main() {
  uv = sourceUv;
  gl_Position = vec4(position, 0.0, 1.0);
}`;

const texturedShapeFragmentShaderSource = `#version 300 es
precision highp float;
uniform vec3 tint;
uniform float alpha;
uniform sampler2D shapeTexture;
in vec2 uv;
out vec4 outColor;
void main() {
  vec4 texel = texture(shapeTexture, fract(uv));
  outColor = vec4(texel.rgb * tint, texel.a * alpha);
}`;

const defaultAudioState = {
  bass: 1,
  bass_att: 1,
  mid: 1,
  mid_att: 1,
  treb: 1,
  treb_att: 1,
};

const clamp01 = (value) => Math.max(0, Math.min(1, Number(value) || 0));

const createShader = (gl, type, source) => {
  const shader = gl.createShader(type);
  gl.shaderSource(shader, source);
  gl.compileShader(shader);
  if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
    throw new Error(gl.getShaderInfoLog(shader) || 'MilkDrop shader compile failed.');
  }
  return shader;
};

const createProgram = (
  gl,
  vertexSource = vertexShaderSource,
  fragmentSource = fragmentShaderSource,
) => {
  const vertexShader = createShader(gl, gl.VERTEX_SHADER, vertexSource);
  const fragmentShader = createShader(gl, gl.FRAGMENT_SHADER, fragmentSource);
  const program = gl.createProgram();
  gl.attachShader(program, vertexShader);
  gl.attachShader(program, fragmentShader);
  gl.linkProgram(program);
  if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
    throw new Error(gl.getProgramInfoLog(program) || 'MilkDrop shader link failed.');
  }
  return program;
};

const createFullscreenTriangle = (gl, program) => {
  const buffer = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
  gl.bufferData(
    gl.ARRAY_BUFFER,
    new Float32Array([
      -1, -1,
      3, -1,
      -1, 3,
    ]),
    gl.STATIC_DRAW,
  );

  const position = gl.getAttribLocation(program, 'position');
  gl.enableVertexAttribArray(position);
  gl.vertexAttribPointer(position, 2, gl.FLOAT, false, 0, 0);
  return buffer;
};

const createOptionalShaderProgram = (gl, shaderSource) => {
  const fragmentSource = createTranslatedMilkdropFragmentShader(shaderSource);
  if (!fragmentSource) return null;
  const program = createProgram(gl, vertexShaderSource, fragmentSource);
  gl.useProgram(program);
  const state = {
    colorUniform: gl.getUniformLocation(program, 'color'),
    feedbackUniform: gl.getUniformLocation(program, 'feedback'),
    outputAlphaUniform: gl.getUniformLocation(program, 'outputAlpha'),
    previousFrameUniform: gl.getUniformLocation(program, 'previousFrame'),
    program,
    timeUniform: gl.getUniformLocation(program, 'time'),
  };
  gl.uniform1i(state.previousFrameUniform, 0);
  return state;
};

const bindTranslatedShaderProgram = (
  gl,
  shaderProgram,
  color,
  feedback,
  time,
  outputAlpha = 1,
) => {
  gl.useProgram(shaderProgram.program);
  gl.uniform3f(shaderProgram.colorUniform, color[0], color[1], color[2]);
  gl.uniform1f(shaderProgram.feedbackUniform, feedback);
  gl.uniform1f(shaderProgram.outputAlphaUniform, outputAlpha);
  gl.uniform1f(shaderProgram.timeUniform, time);
};

const createDynamicLineBuffer = (gl, program) => {
  const positionBuffer = gl.createBuffer();
  const colorBuffer = gl.createBuffer();
  const positionLocation = gl.getAttribLocation(program, 'position');
  const colorLocation = gl.getAttribLocation(program, 'vertexColor');
  return {
    colorBuffer,
    colorLocation,
    positionBuffer,
    positionLocation,
  };
};

const createDynamicWarpGridBuffer = (gl, program) => {
  const positionBuffer = gl.createBuffer();
  const sourceUvBuffer = gl.createBuffer();
  const positionLocation = gl.getAttribLocation(program, 'position');
  const sourceUvLocation = gl.getAttribLocation(program, 'sourceUv');
  return {
    positionBuffer,
    positionLocation,
    sourceUvBuffer,
    sourceUvLocation,
  };
};

const createDynamicTexturedShapeBuffer = (gl, program) => createDynamicWarpGridBuffer(gl, program);

const bindFullscreenTriangle = (gl, program, buffer) => {
  const position = gl.getAttribLocation(program, 'position');
  gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
  gl.enableVertexAttribArray(position);
  gl.vertexAttribPointer(position, 2, gl.FLOAT, false, 0, 0);
};

const bindLineBuffers = (gl, lineBuffers) => {
  gl.bindBuffer(gl.ARRAY_BUFFER, lineBuffers.positionBuffer);
  gl.enableVertexAttribArray(lineBuffers.positionLocation);
  gl.vertexAttribPointer(lineBuffers.positionLocation, 2, gl.FLOAT, false, 0, 0);
  gl.bindBuffer(gl.ARRAY_BUFFER, lineBuffers.colorBuffer);
  gl.enableVertexAttribArray(lineBuffers.colorLocation);
  gl.vertexAttribPointer(lineBuffers.colorLocation, 4, gl.FLOAT, false, 0, 0);
};

const bindWarpGridBuffers = (gl, warpGridBuffers) => {
  gl.bindBuffer(gl.ARRAY_BUFFER, warpGridBuffers.positionBuffer);
  gl.enableVertexAttribArray(warpGridBuffers.positionLocation);
  gl.vertexAttribPointer(warpGridBuffers.positionLocation, 2, gl.FLOAT, false, 0, 0);
  gl.bindBuffer(gl.ARRAY_BUFFER, warpGridBuffers.sourceUvBuffer);
  gl.enableVertexAttribArray(warpGridBuffers.sourceUvLocation);
  gl.vertexAttribPointer(warpGridBuffers.sourceUvLocation, 2, gl.FLOAT, false, 0, 0);
};

const bindTexturedShapeBuffers = (gl, texturedShapeBuffers) =>
  bindWarpGridBuffers(gl, texturedShapeBuffers);

const createProceduralShapeTexture = (gl) => {
  const texture = gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D, texture);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
  gl.texImage2D(
    gl.TEXTURE_2D,
    0,
    gl.RGBA,
    2,
    2,
    0,
    gl.RGBA,
    gl.UNSIGNED_BYTE,
    new Uint8Array([
      255, 255, 255, 255,
      96, 199, 217, 255,
      139, 212, 80, 255,
      242, 184, 75, 255,
    ]),
  );
  return texture;
};

const normalizeTextureName = (value) =>
  String(value || '')
    .trim()
    .replace(/^['"]|['"]$/g, '')
    .replace(/\\/g, '/')
    .toLowerCase();

const getTextureNameAliases = (value) => {
  const normalized = normalizeTextureName(value);
  const basename = normalized.replace(/^.*\//, '');
  const stem = basename.replace(/\.[^.]+$/, '');
  return Array.from(new Set([normalized, basename, stem].filter(Boolean)));
};

const getTextureAssetCandidates = (shape = {}) => [
  shape.baseValues?.texture,
  shape.baseValues?.tex,
  shape.baseValues?.tex_name,
].flatMap(getTextureNameAliases);

const createTextureFromRawAsset = (gl, asset) => {
  const width = Math.max(1, Number(asset.width) || 1);
  const height = Math.max(1, Number(asset.height) || 1);
  const texture = gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D, texture);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
  gl.texImage2D(
    gl.TEXTURE_2D,
    0,
    gl.RGBA,
    width,
    height,
    0,
    gl.RGBA,
    gl.UNSIGNED_BYTE,
    asset.data,
  );
  return texture;
};

const createTextureFromDataUrlAsset = (gl, asset) => {
  const texture = createProceduralShapeTexture(gl);
  if (typeof Image === 'undefined' || !asset.dataUrl) return texture;
  const image = new Image();
  image.onload = () => {
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, image);
  };
  image.src = asset.dataUrl;
  return texture;
};

const createNamedShapeTextures = (gl, textureAssets = {}) => {
  const textures = {};
  Object.entries(textureAssets).forEach(([rawName, asset]) => {
    const aliases = getTextureNameAliases(rawName);
    if (aliases.length === 0 || !asset) return;
    const texture = asset.data
      ? createTextureFromRawAsset(gl, asset)
      : createTextureFromDataUrlAsset(gl, asset);
    aliases.forEach((name) => {
      textures[name] = texture;
    });
  });
  return textures;
};

const getShapeTexture = (shape, namedTextures, fallbackTexture) => {
  const name = getTextureAssetCandidates(shape).find((candidate) => namedTextures[candidate]);
  return name ? namedTextures[name] : fallbackTexture;
};

const normalizeSpectrumSample = (value) => {
  const numeric = Number(value) || 0;
  return clamp01(numeric > 1 ? numeric / 255 : numeric);
};

const createFeedbackTarget = (gl, width, height) => {
  const texture = gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D, texture);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
  gl.texImage2D(
    gl.TEXTURE_2D,
    0,
    gl.RGBA,
    Math.max(1, width),
    Math.max(1, height),
    0,
    gl.RGBA,
    gl.UNSIGNED_BYTE,
    null,
  );

  const framebuffer = gl.createFramebuffer();
  gl.bindFramebuffer(gl.FRAMEBUFFER, framebuffer);
  gl.framebufferTexture2D(
    gl.FRAMEBUFFER,
    gl.COLOR_ATTACHMENT0,
    gl.TEXTURE_2D,
    texture,
    0,
  );
  gl.bindFramebuffer(gl.FRAMEBUFFER, null);

  return { framebuffer, height, texture, width };
};

const resizeFeedbackTarget = (gl, target, width, height) => {
  target.width = Math.max(1, width);
  target.height = Math.max(1, height);
  gl.bindTexture(gl.TEXTURE_2D, target.texture);
  gl.texImage2D(
    gl.TEXTURE_2D,
    0,
    gl.RGBA,
    target.width,
    target.height,
    0,
    gl.RGBA,
    gl.UNSIGNED_BYTE,
    null,
  );
};

export const createMilkdropInitialScope = (preset, frame = {}) => ({
  ...defaultAudioState,
  ...preset.baseValues,
  frame: frame.frame || 0,
  fps: frame.fps || 60,
  time: frame.time || 0,
  wave_b: preset.baseValues.wave_b ?? 0.7,
  wave_g: preset.baseValues.wave_g ?? 0.7,
  wave_r: preset.baseValues.wave_r ?? 0.7,
  ...frame.audio,
});

export const getMilkdropFrameColor = (scope) => [
  clamp01(scope.wave_r ?? scope.q1 ?? 0.7),
  clamp01(scope.wave_g ?? scope.q2 ?? 0.7),
  clamp01(scope.wave_b ?? scope.q3 ?? 0.7),
];

export const getMilkdropWarpState = (scope) => ({
  dx: Number(scope.dx ?? 0),
  dy: Number(scope.dy ?? 0),
  rot: Number(scope.rot ?? 0),
  zoom: Math.max(0.001, Number(scope.zoom ?? 1) || 1),
});

const getWarpedUv = (scope, x, y) => {
  const warp = getMilkdropWarpState(scope);
  const centeredX = x - 0.5;
  const centeredY = y - 0.5;
  const sine = Math.sin(warp.rot);
  const cosine = Math.cos(warp.rot);
  const scaledX = centeredX / warp.zoom;
  const scaledY = centeredY / warp.zoom;
  return [
    (cosine * scaledX) - (sine * scaledY) + 0.5 + warp.dx,
    (sine * scaledX) + (cosine * scaledY) + 0.5 + warp.dy,
  ];
};

const createWarpGridPoint = (scope, equations, x, y) => {
  const centeredX = x - 0.5;
  const centeredY = y - 0.5;
  const pointScope = equations
    ? evaluateMilkdropEquations(equations, {
      ...scope,
      ang: Math.atan2(centeredY, centeredX),
      rad: Math.sqrt((centeredX * centeredX) + (centeredY * centeredY)),
      x,
      y,
    })
    : scope;
  return {
    position: [(x * 2) - 1, (y * 2) - 1],
    sourceUv: getWarpedUv(pointScope, x, y),
  };
};

export const createWarpGridMesh = (scope = {}, equations = '', columns = 8, rows = 6) => {
  const safeColumns = Math.max(1, Math.min(128, Math.floor(columns)));
  const safeRows = Math.max(1, Math.min(128, Math.floor(rows)));
  const positions = [];
  const sourceUvs = [];
  const pushPoint = (point) => {
    positions.push(point.position[0], point.position[1]);
    sourceUvs.push(point.sourceUv[0], point.sourceUv[1]);
  };

  for (let row = 0; row < safeRows; row += 1) {
    for (let column = 0; column < safeColumns; column += 1) {
      const left = column / safeColumns;
      const right = (column + 1) / safeColumns;
      const top = row / safeRows;
      const bottom = (row + 1) / safeRows;
      const topLeft = createWarpGridPoint(scope, equations, left, top);
      const topRight = createWarpGridPoint(scope, equations, right, top);
      const bottomLeft = createWarpGridPoint(scope, equations, left, bottom);
      const bottomRight = createWarpGridPoint(scope, equations, right, bottom);

      pushPoint(topLeft);
      pushPoint(bottomLeft);
      pushPoint(topRight);
      pushPoint(topRight);
      pushPoint(bottomLeft);
      pushPoint(bottomRight);
    }
  }

  return {
    positions: new Float32Array(positions),
    sourceUvs: new Float32Array(sourceUvs),
    vertexCount: positions.length / 2,
  };
};

export const createWaveformVertices = (samples = [], scale = 1) => {
  const count = Math.max(0, samples.length);
  if (count < 2) return new Float32Array();

  const vertices = new Float32Array(count * 2);
  samples.forEach((sample, index) => {
    const x = count === 1 ? 0 : (index / (count - 1)) * 2 - 1;
    const y = clamp01(0.5 + (Number(sample) || 0) * scale * 0.5) * 2 - 1;
    vertices[index * 2] = x;
    vertices[index * 2 + 1] = y;
  });
  return vertices;
};

export const createMotionVectorVertices = (scope = {}) => {
  const columns = Math.max(0, Math.min(128, Math.floor(Number(scope.mv_x ?? 0))));
  const rows = Math.max(0, Math.min(128, Math.floor(Number(scope.mv_y ?? 0))));
  if (columns < 1 || rows < 1) return new Float32Array();

  const deltaX = Number(scope.mv_dx ?? 0.02);
  const deltaY = Number(scope.mv_dy ?? 0.02);
  const length = Math.max(0, Number(scope.mv_l ?? 0.05) || 0.05);
  const vertices = new Float32Array(columns * rows * 4);
  let offset = 0;

  for (let row = 0; row < rows; row += 1) {
    for (let column = 0; column < columns; column += 1) {
      const x = columns === 1 ? 0 : (column / (columns - 1)) * 2 - 1;
      const y = rows === 1 ? 0 : (row / (rows - 1)) * 2 - 1;
      vertices[offset] = x;
      vertices[offset + 1] = y;
      vertices[offset + 2] = x + deltaX * length * 2;
      vertices[offset + 3] = y + deltaY * length * 2;
      offset += 4;
    }
  }

  return vertices;
};

export const getMotionVectorColor = (scope = {}, fallbackColor = [0.7, 0.7, 0.7]) => [
  clamp01(scope.mv_r ?? fallbackColor[0]),
  clamp01(scope.mv_g ?? fallbackColor[1]),
  clamp01(scope.mv_b ?? fallbackColor[2]),
  clamp01(scope.mv_a ?? 0.8),
];

export const createShapeVertices = (shape = {}) => {
  if (!Number(shape.baseValues?.enabled ?? 0)) {
    return new Float32Array();
  }

  const sides = Math.max(3, Math.min(500, Math.floor(Number(shape.baseValues.sides ?? 4))));
  const radius = Math.max(0, Number(shape.baseValues.rad ?? shape.baseValues.radius ?? 0.1));
  const centerX = (Number(shape.baseValues.x ?? 0.5) * 2) - 1;
  const centerY = (Number(shape.baseValues.y ?? 0.5) * 2) - 1;
  const angle = Number(shape.baseValues.ang ?? 0);
  const vertices = new Float32Array((sides + 1) * 2);

  for (let index = 0; index <= sides; index += 1) {
    const theta = angle + (index / sides) * Math.PI * 2;
    vertices[index * 2] = centerX + Math.cos(theta) * radius;
    vertices[index * 2 + 1] = centerY + Math.sin(theta) * radius;
  }

  return vertices;
};

export const createShapeFillVertices = (shape = {}) => {
  const outline = createShapeVertices(shape);
  if (outline.length === 0) return outline;

  const vertices = new Float32Array(outline.length + 2);
  vertices[0] = (Number(shape.baseValues.x ?? 0.5) * 2) - 1;
  vertices[1] = (Number(shape.baseValues.y ?? 0.5) * 2) - 1;
  vertices.set(outline, 2);
  return vertices;
};

export const isShapeTextured = (shape = {}) =>
  Number(shape.baseValues?.textured ?? 0) > 0
  || Boolean(shape.baseValues?.texture || shape.baseValues?.tex_name || shape.baseValues?.tex);

export const createShapeTextureUvs = (shape = {}) => {
  const vertexCount = createShapeFillVertices(shape).length / 2;
  if (!vertexCount) return new Float32Array();
  const zoom = Math.max(0.001, Number(shape.baseValues?.tex_zoom ?? 1) || 1);
  const angle = Number(shape.baseValues?.tex_ang ?? 0);
  const sine = Math.sin(angle);
  const cosine = Math.cos(angle);
  const uvs = new Float32Array(vertexCount * 2);
  uvs[0] = 0.5;
  uvs[1] = 0.5;

  for (let index = 1; index < vertexCount; index += 1) {
    const progress = (index - 1) / Math.max(1, vertexCount - 2);
    const theta = progress * Math.PI * 2;
    const radius = 0.5 / zoom;
    const x = Math.cos(theta) * radius;
    const y = Math.sin(theta) * radius;
    uvs[index * 2] = 0.5 + (cosine * x) - (sine * y);
    uvs[index * 2 + 1] = 0.5 + (sine * x) + (cosine * y);
  }

  return uvs;
};

export const getShapeColor = (shape = {}, fallbackColor = [0.7, 0.7, 0.7]) => [
  clamp01(shape.baseValues?.r ?? fallbackColor[0]),
  clamp01(shape.baseValues?.g ?? fallbackColor[1]),
  clamp01(shape.baseValues?.b ?? fallbackColor[2]),
];

export const getShapeFillColor = (shape = {}, fallbackColor = [0.7, 0.7, 0.7]) => [
  ...getShapeColor(shape, fallbackColor),
  clamp01(shape.baseValues?.a ?? 0.6),
];

export const getShapeFillEdgeColor = (shape = {}, fallbackColor = [0.7, 0.7, 0.7]) => [
  clamp01(shape.baseValues?.r2 ?? shape.baseValues?.r ?? fallbackColor[0]),
  clamp01(shape.baseValues?.g2 ?? shape.baseValues?.g ?? fallbackColor[1]),
  clamp01(shape.baseValues?.b2 ?? shape.baseValues?.b ?? fallbackColor[2]),
  clamp01(shape.baseValues?.a2 ?? shape.baseValues?.a ?? 0.6),
];

export const getShapeBorderColor = (shape = {}, fallbackColor = [0.7, 0.7, 0.7]) => [
  clamp01(shape.baseValues?.border_r ?? shape.baseValues?.r ?? fallbackColor[0]),
  clamp01(shape.baseValues?.border_g ?? shape.baseValues?.g ?? fallbackColor[1]),
  clamp01(shape.baseValues?.border_b ?? shape.baseValues?.b ?? fallbackColor[2]),
  clamp01(shape.baseValues?.border_a ?? 0.85),
];

export const createRepeatedColors = (vertexCount, color) => {
  const colors = new Float32Array(Math.max(0, vertexCount) * 4);
  for (let index = 0; index < vertexCount; index += 1) {
    colors.set(color, index * 4);
  }
  return colors;
};

export const createShapeFillColors = (shape = {}, fallbackColor = [0.7, 0.7, 0.7]) => {
  const vertexCount = createShapeFillVertices(shape).length / 2;
  if (!vertexCount) return new Float32Array();
  const colors = new Float32Array(vertexCount * 4);
  colors.set(getShapeFillColor(shape, fallbackColor), 0);
  const edgeColor = getShapeFillEdgeColor(shape, fallbackColor);
  for (let index = 1; index < vertexCount; index += 1) {
    colors.set(edgeColor, index * 4);
  }
  return colors;
};

const shapeValueKeys = new Set([
  'a',
  'a2',
  'additive',
  'ang',
  'b',
  'b2',
  'border_a',
  'border_b',
  'border_g',
  'border_r',
  'enabled',
  'g',
  'g2',
  'r',
  'r2',
  'rad',
  'radius',
  'sides',
  'tex',
  'tex_ang',
  'tex_name',
  'tex_zoom',
  'texture',
  'textured',
  'thickoutline',
  'x',
  'y',
]);

const waveValueKeys = new Set([
  'a',
  'additive',
  'b',
  'dots',
  'enabled',
  'g',
  'r',
  'samples',
  'scaling',
  'spectrum',
  'thick',
]);

const isQVariable = (key) => /^q([1-9]|[1-5][0-9]|6[0-4])$/.test(key);

const persistScopedValues = (baseValues, scope, allowedKeys) => {
  const nextBaseValues = { ...baseValues };
  Object.entries(scope).forEach(([key, value]) => {
    if (allowedKeys.has(key) || isQVariable(key)) {
      nextBaseValues[key] = value;
    }
  });
  return nextBaseValues;
};

export const evaluateShapeState = (shape = {}, frameScope = {}) => {
  let shapeScope = {
    ...frameScope,
    ...shape.baseValues,
  };
  if (shape.equations?.init && !shape.initialized) {
    shapeScope = evaluateMilkdropEquations(shape.equations.init, shapeScope);
    shape.initialized = true;
  }
  if (shape.equations?.frame) {
    shapeScope = evaluateMilkdropEquations(shape.equations.frame, shapeScope);
  }
  shape.baseValues = persistScopedValues(shape.baseValues, shapeScope, shapeValueKeys);
  return shape;
};

export const evaluateWaveState = (wave = {}, frameScope = {}) => {
  let waveScope = {
    ...frameScope,
    ...wave.baseValues,
  };
  if (wave.equations?.init && !wave.initialized) {
    waveScope = evaluateMilkdropEquations(wave.equations.init, waveScope);
    wave.initialized = true;
  }
  if (wave.equations?.frame) {
    waveScope = evaluateMilkdropEquations(wave.equations.frame, waveScope);
  }
  wave.baseValues = persistScopedValues(wave.baseValues, waveScope, waveValueKeys);
  return wave;
};

export const createCustomWaveVertices = (wave = {}, samples = [], frameScope = {}) => {
  if (!Number(wave.baseValues?.enabled ?? 0)) {
    return new Float32Array();
  }

  const sampleCount = Math.max(
    2,
    Math.min(4096, Math.floor(Number(wave.baseValues.samples ?? samples.length ?? 2) || 2)),
  );
  const fallbackSamples = samples.length > 0 ? samples : [0, 0];
  const useSpectrum = Number(wave.baseValues.spectrum ?? 0) > 0;
  const vertices = new Float32Array(sampleCount * 2);

  for (let index = 0; index < sampleCount; index += 1) {
    const progress = sampleCount === 1 ? 0 : index / (sampleCount - 1);
    const sampleIndex = Math.min(
      fallbackSamples.length - 1,
      Math.floor(progress * fallbackSamples.length),
    );
    const rawSample = Number(fallbackSamples[sampleIndex]) || 0;
    const sample = useSpectrum ? normalizeSpectrumSample(rawSample) : rawSample;
    let pointScope = {
      ...frameScope,
      ...wave.baseValues,
      i: progress,
      sample,
      value: sample,
      x: progress,
      y: useSpectrum ? sample : clamp01(0.5 + sample * 0.5),
    };

    if (wave.equations?.point) {
      pointScope = evaluateMilkdropEquations(wave.equations.point, pointScope);
    }

    vertices[index * 2] = (Number(pointScope.x ?? progress) * 2) - 1;
    vertices[index * 2 + 1] = (Number(pointScope.y ?? 0.5) * 2) - 1;
  }

  return vertices;
};

export const getWaveColor = (wave = {}, fallbackColor = [0.7, 0.7, 0.7]) => [
  clamp01(wave.baseValues?.r ?? fallbackColor[0]),
  clamp01(wave.baseValues?.g ?? fallbackColor[1]),
  clamp01(wave.baseValues?.b ?? fallbackColor[2]),
  clamp01(wave.baseValues?.a ?? 1),
];

export const getWaveDrawMode = (wave = {}, gl) =>
  (Number(wave.baseValues?.dots ?? 0) > 0 ? gl.POINTS : gl.LINE_STRIP);

export const getWavePointSize = (wave = {}) => {
  if (Number(wave.baseValues?.dots ?? 0) <= 0) return 1;
  return Number(wave.baseValues?.thick ?? 0) > 0 ? 4 : 2;
};

export const createMilkdropRenderer = ({ canvas, preset, textureAssets = {} }) => {
  const gl = canvas.getContext('webgl2', {
    alpha: false,
    antialias: false,
    depth: false,
    premultipliedAlpha: false,
  });
  if (!gl) {
    throw new Error('WebGL2 is required for the native MilkDrop renderer.');
  }

  const program = createProgram(gl);
  gl.useProgram(program);
  const fullscreenBuffer = createFullscreenTriangle(gl, program);
  const translatedWarpProgram = createOptionalShaderProgram(gl, preset.shaders?.warp);
  const translatedCompProgram = createOptionalShaderProgram(gl, preset.shaders?.comp);
  const warpGridProgram = createProgram(
    gl,
    warpGridVertexShaderSource,
    warpGridFragmentShaderSource,
  );
  gl.useProgram(warpGridProgram);
  const warpGridBuffers = createDynamicWarpGridBuffer(gl, warpGridProgram);
  const lineProgram = createProgram(gl, lineVertexShaderSource, lineFragmentShaderSource);
  gl.useProgram(lineProgram);
  const lineBuffers = createDynamicLineBuffer(gl, lineProgram);
  const pointSizeUniform = gl.getUniformLocation(lineProgram, 'pointSize');
  const texturedShapeProgram = createProgram(
    gl,
    texturedShapeVertexShaderSource,
    texturedShapeFragmentShaderSource,
  );
  gl.useProgram(texturedShapeProgram);
  const texturedShapeBuffers = createDynamicTexturedShapeBuffer(gl, texturedShapeProgram);
  const texturedShapeAlphaUniform = gl.getUniformLocation(texturedShapeProgram, 'alpha');
  const texturedShapeSamplerUniform = gl.getUniformLocation(texturedShapeProgram, 'shapeTexture');
  const texturedShapeTintUniform = gl.getUniformLocation(texturedShapeProgram, 'tint');
  const proceduralShapeTexture = createProceduralShapeTexture(gl);
  const namedShapeTextures = createNamedShapeTextures(gl, textureAssets);
  gl.uniform1i(texturedShapeSamplerUniform, 1);
  gl.useProgram(program);
  const colorUniform = gl.getUniformLocation(program, 'color');
  const feedbackUniform = gl.getUniformLocation(program, 'feedback');
  const outputAlphaUniform = gl.getUniformLocation(program, 'outputAlpha');
  const previousFrameUniform = gl.getUniformLocation(program, 'previousFrame');
  const rotationUniform = gl.getUniformLocation(program, 'rotation');
  const translateUniform = gl.getUniformLocation(program, 'translate');
  const zoomUniform = gl.getUniformLocation(program, 'zoom');
  gl.useProgram(warpGridProgram);
  const warpGridColorUniform = gl.getUniformLocation(warpGridProgram, 'color');
  const warpGridFeedbackUniform = gl.getUniformLocation(warpGridProgram, 'feedback');
  const warpGridOutputAlphaUniform = gl.getUniformLocation(warpGridProgram, 'outputAlpha');
  const warpGridPreviousFrameUniform = gl.getUniformLocation(warpGridProgram, 'previousFrame');
  gl.useProgram(program);
  const feedbackTargets = [
    createFeedbackTarget(gl, canvas.width, canvas.height),
    createFeedbackTarget(gl, canvas.width, canvas.height),
  ];
  let readTarget = feedbackTargets[0];
  let writeTarget = feedbackTargets[1];
  let initialized = false;
  let scope = createMilkdropInitialScope(preset);

  gl.uniform1i(previousFrameUniform, 0);
  gl.useProgram(warpGridProgram);
  gl.uniform1i(warpGridPreviousFrameUniform, 0);
  gl.useProgram(program);

  return {
    name: 'slskdN MilkDrop WebGL',
    dispose: () => {
      feedbackTargets.forEach((target) => {
        gl.deleteFramebuffer(target.framebuffer);
        gl.deleteTexture(target.texture);
      });
      gl.deleteProgram(program);
      if (translatedWarpProgram) gl.deleteProgram(translatedWarpProgram.program);
      if (translatedCompProgram) gl.deleteProgram(translatedCompProgram.program);
      gl.deleteProgram(warpGridProgram);
      gl.deleteProgram(lineProgram);
      gl.deleteProgram(texturedShapeProgram);
      gl.deleteTexture(proceduralShapeTexture);
      Array.from(new Set(Object.values(namedShapeTextures))).forEach((texture) =>
        gl.deleteTexture(texture));
    },
    render: (frame = {}, options = {}) => {
      const clearScreen = options.clearScreen !== false;
      const outputAlpha = clamp01(options.outputAlpha ?? 1);
      const frequencyData = frame.spectrum || frame.frequencies || frame.frequency || frame.fft || [];
      scope = {
        ...scope,
        frequency_data: frequencyData,
        frame: frame.frame ?? scope.frame + 1,
        fps: frame.fps ?? scope.fps,
        sample_rate: frame.sampleRate ?? frame.sample_rate ?? scope.sample_rate ?? 44100,
        time: frame.time ?? scope.time,
        ...frame.audio,
      };

      if (!initialized && preset.equations.init) {
        scope = evaluateMilkdropEquations(preset.equations.init, scope);
        initialized = true;
      }
      if (preset.equations.perFrame) {
        scope = evaluateMilkdropEquations(preset.equations.perFrame, scope);
      }

      const [r, g, b] = getMilkdropFrameColor(scope);
      const feedback = clamp01(scope.decay ?? 0.9);
      const warp = getMilkdropWarpState(scope);
      gl.viewport(0, 0, canvas.width, canvas.height);
      gl.activeTexture(gl.TEXTURE0);
      gl.bindTexture(gl.TEXTURE_2D, readTarget.texture);
      gl.bindFramebuffer(gl.FRAMEBUFFER, writeTarget.framebuffer);
      gl.clearColor(0, 0, 0, 1);
      gl.clear(gl.COLOR_BUFFER_BIT);
      if (preset.equations.perPixel) {
        const warpGridMesh = createWarpGridMesh(
          scope,
          preset.equations.perPixel,
          Number(scope.meshx ?? 8) || 8,
          Number(scope.meshy ?? 6) || 6,
        );
        gl.useProgram(warpGridProgram);
        bindWarpGridBuffers(gl, warpGridBuffers);
        gl.uniform3f(warpGridColorUniform, r, g, b);
        gl.uniform1f(warpGridFeedbackUniform, feedback);
        gl.uniform1f(warpGridOutputAlphaUniform, 1);
        gl.bindBuffer(gl.ARRAY_BUFFER, warpGridBuffers.positionBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, warpGridMesh.positions, gl.DYNAMIC_DRAW);
        gl.bindBuffer(gl.ARRAY_BUFFER, warpGridBuffers.sourceUvBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, warpGridMesh.sourceUvs, gl.DYNAMIC_DRAW);
        gl.drawArrays(gl.TRIANGLES, 0, warpGridMesh.vertexCount);
        gl.useProgram(program);
      } else if (translatedWarpProgram) {
        bindTranslatedShaderProgram(
          gl,
          translatedWarpProgram,
          [r, g, b],
          feedback,
          scope.time,
          1,
        );
        bindFullscreenTriangle(gl, translatedWarpProgram.program, fullscreenBuffer);
        gl.drawArrays(gl.TRIANGLES, 0, 3);
        gl.useProgram(program);
      } else {
        gl.useProgram(program);
        gl.uniform3f(colorUniform, r, g, b);
        gl.uniform1f(feedbackUniform, feedback);
        gl.uniform1f(outputAlphaUniform, 1);
        gl.uniform1f(rotationUniform, warp.rot);
        gl.uniform1f(zoomUniform, warp.zoom);
        gl.uniform2f(translateUniform, warp.dx, warp.dy);
        bindFullscreenTriangle(gl, program, fullscreenBuffer);
        gl.drawArrays(gl.TRIANGLES, 0, 3);
      }

      const waveformVertices = createWaveformVertices(
        frame.waveform || frame.samples || [],
        Number(scope.wave_scale ?? 1) || 1,
      );
      if (waveformVertices.length > 0) {
        const waveformColors = createRepeatedColors(waveformVertices.length / 2, [r, g, b, 1]);
        gl.useProgram(lineProgram);
        bindLineBuffers(gl, lineBuffers);
        gl.uniform1f(pointSizeUniform, 1);
        gl.bindBuffer(gl.ARRAY_BUFFER, lineBuffers.positionBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, waveformVertices, gl.DYNAMIC_DRAW);
        gl.bindBuffer(gl.ARRAY_BUFFER, lineBuffers.colorBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, waveformColors, gl.DYNAMIC_DRAW);
        gl.drawArrays(gl.LINE_STRIP, 0, waveformVertices.length / 2);
        gl.useProgram(program);
      }

      const motionVectorVertices = createMotionVectorVertices(scope);
      if (motionVectorVertices.length > 0) {
        const motionVectorColor = getMotionVectorColor(scope, [r, g, b]);
        gl.enable(gl.BLEND);
        gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
        gl.useProgram(lineProgram);
        bindLineBuffers(gl, lineBuffers);
        gl.uniform1f(pointSizeUniform, 1);
        gl.bindBuffer(gl.ARRAY_BUFFER, lineBuffers.positionBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, motionVectorVertices, gl.DYNAMIC_DRAW);
        gl.bindBuffer(gl.ARRAY_BUFFER, lineBuffers.colorBuffer);
        gl.bufferData(
          gl.ARRAY_BUFFER,
          createRepeatedColors(motionVectorVertices.length / 2, motionVectorColor),
          gl.DYNAMIC_DRAW,
        );
        gl.drawArrays(gl.LINES, 0, motionVectorVertices.length / 2);
        gl.disable(gl.BLEND);
        gl.useProgram(program);
      }

      preset.waves.forEach((wave) => {
        const evaluatedWave = evaluateWaveState(wave, scope);
        const waveSamples = Number(evaluatedWave.baseValues?.spectrum ?? 0) > 0
          ? frequencyData.length > 0 ? frequencyData : frame.waveform || frame.samples || []
          : frame.waveform || frame.samples || [];
        const customWaveVertices = createCustomWaveVertices(
          evaluatedWave,
          waveSamples,
          scope,
        );
        if (customWaveVertices.length === 0) return;
        const customWaveColor = getWaveColor(evaluatedWave, [r, g, b]);
        const customWaveColors = createRepeatedColors(
          customWaveVertices.length / 2,
          customWaveColor,
        );
        const additive = Number(evaluatedWave.baseValues?.additive ?? 0) > 0;
        const thick = Number(evaluatedWave.baseValues?.thick ?? 0) > 0;
        gl.enable(gl.BLEND);
        gl.blendFunc(gl.SRC_ALPHA, additive ? gl.ONE : gl.ONE_MINUS_SRC_ALPHA);
        gl.lineWidth(thick ? 2 : 1);
        gl.useProgram(lineProgram);
        bindLineBuffers(gl, lineBuffers);
        gl.uniform1f(pointSizeUniform, getWavePointSize(evaluatedWave));
        gl.bindBuffer(gl.ARRAY_BUFFER, lineBuffers.positionBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, customWaveVertices, gl.DYNAMIC_DRAW);
        gl.bindBuffer(gl.ARRAY_BUFFER, lineBuffers.colorBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, customWaveColors, gl.DYNAMIC_DRAW);
        gl.drawArrays(
          getWaveDrawMode(evaluatedWave, gl),
          0,
          customWaveVertices.length / 2,
        );
        gl.lineWidth(1);
        gl.disable(gl.BLEND);
        gl.useProgram(program);
      });

      preset.shapes.forEach((shape) => {
        const evaluatedShape = evaluateShapeState(shape, scope);
        const shapeFillVertices = createShapeFillVertices(evaluatedShape);
        const shapeVertices = createShapeVertices(evaluatedShape);
        if (shapeFillVertices.length === 0 && shapeVertices.length === 0) return;
        const additive = Number(evaluatedShape.baseValues?.additive ?? 0) > 0;
        gl.enable(gl.BLEND);
        gl.blendFunc(gl.SRC_ALPHA, additive ? gl.ONE : gl.ONE_MINUS_SRC_ALPHA);
        gl.useProgram(lineProgram);
        bindLineBuffers(gl, lineBuffers);
        gl.uniform1f(pointSizeUniform, 1);
        if (shapeFillVertices.length > 0) {
          if (isShapeTextured(evaluatedShape)) {
            const fillColor = getShapeFillColor(evaluatedShape, [r, g, b]);
            const textureUvs = createShapeTextureUvs(evaluatedShape);
            gl.activeTexture(gl.TEXTURE1 ?? gl.TEXTURE0);
            gl.bindTexture(
              gl.TEXTURE_2D,
              getShapeTexture(evaluatedShape, namedShapeTextures, proceduralShapeTexture),
            );
            gl.useProgram(texturedShapeProgram);
            bindTexturedShapeBuffers(gl, texturedShapeBuffers);
            gl.uniform3f(texturedShapeTintUniform, fillColor[0], fillColor[1], fillColor[2]);
            gl.uniform1f(texturedShapeAlphaUniform, fillColor[3]);
            gl.bindBuffer(gl.ARRAY_BUFFER, texturedShapeBuffers.positionBuffer);
            gl.bufferData(gl.ARRAY_BUFFER, shapeFillVertices, gl.DYNAMIC_DRAW);
            gl.bindBuffer(gl.ARRAY_BUFFER, texturedShapeBuffers.sourceUvBuffer);
            gl.bufferData(gl.ARRAY_BUFFER, textureUvs, gl.DYNAMIC_DRAW);
            gl.drawArrays(gl.TRIANGLE_FAN, 0, shapeFillVertices.length / 2);
            gl.activeTexture(gl.TEXTURE0);
            gl.useProgram(lineProgram);
            bindLineBuffers(gl, lineBuffers);
          } else {
            const shapeFillColors = createShapeFillColors(evaluatedShape, [r, g, b]);
            gl.bindBuffer(gl.ARRAY_BUFFER, lineBuffers.positionBuffer);
            gl.bufferData(gl.ARRAY_BUFFER, shapeFillVertices, gl.DYNAMIC_DRAW);
            gl.bindBuffer(gl.ARRAY_BUFFER, lineBuffers.colorBuffer);
            gl.bufferData(gl.ARRAY_BUFFER, shapeFillColors, gl.DYNAMIC_DRAW);
            gl.drawArrays(gl.TRIANGLE_FAN, 0, shapeFillVertices.length / 2);
          }
        }
        if (shapeVertices.length > 0) {
          const borderColor = getShapeBorderColor(evaluatedShape, [r, g, b]);
          const borderA = borderColor[3];
          if (borderA > 0) {
            const borderWidth = Number(evaluatedShape.baseValues?.thickoutline ?? 0) > 0 ? 2 : 1;
            gl.lineWidth(borderWidth);
            gl.bindBuffer(gl.ARRAY_BUFFER, lineBuffers.positionBuffer);
            gl.bufferData(gl.ARRAY_BUFFER, shapeVertices, gl.DYNAMIC_DRAW);
            gl.bindBuffer(gl.ARRAY_BUFFER, lineBuffers.colorBuffer);
            gl.bufferData(
              gl.ARRAY_BUFFER,
              createRepeatedColors(shapeVertices.length / 2, borderColor),
              gl.DYNAMIC_DRAW,
            );
            gl.drawArrays(gl.LINE_STRIP, 0, shapeVertices.length / 2);
            gl.lineWidth(1);
          }
        }
        gl.disable(gl.BLEND);
        gl.useProgram(program);
      });

      gl.bindTexture(gl.TEXTURE_2D, writeTarget.texture);
      gl.bindFramebuffer(gl.FRAMEBUFFER, null);
      if (clearScreen) {
        gl.clear(gl.COLOR_BUFFER_BIT);
      }
      if (!clearScreen || outputAlpha < 1) {
        gl.enable(gl.BLEND);
        gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
      }
      if (translatedCompProgram) {
        bindTranslatedShaderProgram(
          gl,
          translatedCompProgram,
          [r, g, b],
          0,
          scope.time,
          outputAlpha,
        );
        bindFullscreenTriangle(gl, translatedCompProgram.program, fullscreenBuffer);
      } else {
        gl.useProgram(program);
        gl.uniform3f(colorUniform, 0, 0, 0);
        gl.uniform1f(feedbackUniform, 1);
        gl.uniform1f(outputAlphaUniform, outputAlpha);
        gl.uniform1f(rotationUniform, 0);
        gl.uniform1f(zoomUniform, 1);
        gl.uniform2f(translateUniform, 0, 0);
        bindFullscreenTriangle(gl, program, fullscreenBuffer);
      }
      gl.drawArrays(gl.TRIANGLES, 0, 3);
      if (!clearScreen || outputAlpha < 1) {
        gl.disable(gl.BLEND);
      }

      [readTarget, writeTarget] = [writeTarget, readTarget];
      return scope;
    },
    resize: (width, height) => {
      canvas.width = Math.max(1, Math.floor(width));
      canvas.height = Math.max(1, Math.floor(height));
      feedbackTargets.forEach((target) =>
        resizeFeedbackTarget(gl, target, canvas.width, canvas.height));
    },
  };
};
