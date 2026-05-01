const unsupportedPatterns = [
  /\bfor\s*\(/i,
  /\bwhile\s*\(/i,
  /\bif\s*\(/i,
  /\bfloat[234]x[234]\b/i,
  /\bmul\s*\(/i,
  /\bsampler(?:1d|2d|3d|cube)?\s+[A-Za-z_]/i,
];

const allowedExpressionPattern = /^[A-Za-z0-9_.,+\-*/%<>=!&|^~?:()\s]+$/;
const declarationPattern = /^(float|float2|float3|float4|vec2|vec3|vec4)\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+)$/i;
const assignmentPattern = /^([A-Za-z_][A-Za-z0-9_]*)\s*(=|\+=|-=|\*=|\/=)\s*(.+)$/i;
const shaderTextureLimit = 4;
const shaderTextureCallPattern = /\b(?:tex2D|tex)\s*\(\s*([A-Za-z_][A-Za-z0-9_]*)\s*,/gi;
const shaderQRegisterNames = Array.from({ length: 64 }, (_unused, index) => `q${index + 1}`);
const shaderAudioVariableNames = ['bass', 'bass_att', 'mid', 'mid_att', 'treb', 'treb_att'];
const shaderVariableNames = [...shaderAudioVariableNames, ...shaderQRegisterNames];

const stripShaderComments = (source) =>
  String(source || '')
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/\/\/.*$/gm, '')
    .trim();

const unwrapShaderBody = (source) =>
  stripShaderComments(source)
    .replace(/\bshader_body\s*\{/gi, '')
    .replace(/^\s*\{/, '')
    .replace(/\}\s*$/g, '')
    .trim();

const normalizeSimpleConditionalReturn = (source) => {
  const unwrapped = unwrapShaderBody(source);
  const match = /^if\s*\(([^{};]+)\)\s*\{?\s*ret\s*=\s*([^;{}]+);\s*\}?\s*else\s*\{?\s*ret\s*=\s*([^;{}]+);\s*\}?\s*$/i
    .exec(unwrapped);
  if (!match) return source;
  return `ret = (${match[1].trim()}) ? (${match[2].trim()}) : (${match[3].trim()});`;
};

const isMainSampler = (name) =>
  ['previousframe', 'sampler_main', 'sampler_fc_main', 'sampler_sampler_main'].includes(
    String(name || '').toLowerCase(),
  );

export const getMilkdropShaderTextureSamplers = (source) => {
  const samplers = [];
  let match = shaderTextureCallPattern.exec(stripShaderComments(source));
  while (match) {
    const sampler = match[1];
    if (!isMainSampler(sampler) && !samplers.includes(sampler)) {
      samplers.push(sampler);
    }
    match = shaderTextureCallPattern.exec(stripShaderComments(source));
  }
  return samplers.slice(0, shaderTextureLimit);
};

const getShaderTextureUniformName = (samplers, sampler) => {
  const index = samplers.indexOf(sampler);
  return index >= 0 ? `shaderTexture${index}` : '';
};

const normalizeShaderSource = (source, textureSamplers = []) =>
  unwrapShaderBody(normalizeSimpleConditionalReturn(source))
    .replace(shaderTextureCallPattern, (_match, sampler) => {
      if (isMainSampler(sampler)) return 'texture(previousFrame,';
      const textureUniform = getShaderTextureUniformName(textureSamplers, sampler);
      return textureUniform ? `texture(${textureUniform},` : `texture(${sampler},`;
    });

const normalizeShaderExpression = (expression) =>
  expression
    .replace(/\bfloat4\s*\(/gi, 'vec4(')
    .replace(/\bfloat3\s*\(/gi, 'vec3(')
    .replace(/\bfloat2\s*\(/gi, 'vec2(')
    .replace(/\bsaturate\s*\(/gi, 'clamp01(')
    .replace(/\blerp\s*\(/gi, 'mix(')
    .replace(/\bfrac\s*\(/gi, 'fract(')
    .replace(/\bfmod\s*\(/gi, 'mod(')
    .replace(/\brsqrt\s*\(/gi, 'inversesqrt(')
    .replace(/\batan2\s*\(/gi, 'atan(');

const normalizeShaderType = (type) =>
  type.toLowerCase()
    .replace('float2', 'vec2')
    .replace('float3', 'vec3')
    .replace('float4', 'vec4');

const isSafeShaderExpression = (expression) => {
  if (!expression) return false;
  if (!allowedExpressionPattern.test(expression)) return false;
  return !(/\btexture\s*\(/i.test(expression)
    && !/\b(?:previousFrame|shaderTexture[0-3])\b/.test(expression));
};

const parseShaderProgram = (source) => {
  const normalizedSource = normalizeSimpleConditionalReturn(source);
  if (unsupportedPatterns.some((pattern) => pattern.test(normalizedSource))) return null;
  const textureSamplers = getMilkdropShaderTextureSamplers(normalizedSource);
  const cleaned = normalizeShaderSource(normalizedSource, textureSamplers);
  const statements = cleaned
    .split(';')
    .map((statement) => statement.trim())
    .filter(Boolean);
  const declarations = [];
  const mutableVariables = new Set();
  let expression = '';

  for (const statement of statements) {
    const retMatch = /^ret\s*=\s*(.+)$/i.exec(statement);
    if (retMatch) {
      if (expression) return null;
      expression = normalizeShaderExpression(retMatch[1].trim());
      continue;
    }

    if (expression) return null;

    const declarationMatch = declarationPattern.exec(statement);
    if (declarationMatch) {
      const declarationExpression = normalizeShaderExpression(declarationMatch[3].trim());
      if (!isSafeShaderExpression(declarationExpression)) return null;
      mutableVariables.add(declarationMatch[2]);
      declarations.push(
        `${normalizeShaderType(declarationMatch[1])} ${declarationMatch[2]} = ${declarationExpression};`,
      );
      continue;
    }

    const assignmentMatch = assignmentPattern.exec(statement);
    if (assignmentMatch && mutableVariables.has(assignmentMatch[1])) {
      const assignmentExpression = normalizeShaderExpression(assignmentMatch[3].trim());
      if (!isSafeShaderExpression(assignmentExpression)) return null;
      declarations.push(`${assignmentMatch[1]} ${assignmentMatch[2]} ${assignmentExpression};`);
      continue;
    }

    return null;
  }

  if (!isSafeShaderExpression(expression)) return null;
  return {
    declarations,
    expression,
    textureSamplers,
  };
};

export const translateMilkdropShaderExpression = (source) => {
  const parsed = parseShaderProgram(source);
  return parsed?.expression || '';
};

export const createTranslatedMilkdropFragmentShader = (source) => {
  const parsed = parseShaderProgram(source);
  if (!parsed) return '';
  const uniformDeclarations = shaderVariableNames
    .map((name) => `uniform float ${name};`)
    .join('\n');
  const textureUniformDeclarations = parsed.textureSamplers
    .map((_sampler, index) => `uniform sampler2D shaderTexture${index};`)
    .join('\n');
  return `#version 300 es
precision highp float;
uniform vec3 color;
uniform sampler2D previousFrame;
${textureUniformDeclarations}
uniform float feedback;
uniform float outputAlpha;
uniform float time;
uniform float sampleRate;
uniform float fftBins[64];
uniform float waveformBins[64];
uniform vec2 resolution;
uniform vec2 pixelSize;
uniform float aspect;
uniform vec4 texsize;
${uniformDeclarations}
in vec2 uv;
out vec4 outColor;
float clamp01(float value) {
  return clamp(value, 0.0, 1.0);
}
vec2 clamp01(vec2 value) {
  return clamp(value, vec2(0.0), vec2(1.0));
}
vec3 clamp01(vec3 value) {
  return clamp(value, vec3(0.0), vec3(1.0));
}
vec4 clamp01(vec4 value) {
  return clamp(value, vec4(0.0), vec4(1.0));
}
float get_fft(float position) {
  int index = int(clamp(position, 0.0, 1.0) * 63.0);
  return fftBins[index];
}
float get_fft_hz(float hz) {
  float nyquist = max(sampleRate * 0.5, 1.0);
  return get_fft(hz / nyquist);
}
float get_waveform(float position) {
  int index = int(clamp(position, 0.0, 1.0) * 63.0);
  return waveformBins[index];
}
void main() {
  float x = uv.x;
  float y = uv.y;
  vec2 centeredUv = uv - vec2(0.5);
  float rad = length(centeredUv);
  float ang = atan(centeredUv.y, centeredUv.x);
  ${parsed.declarations.join('\n  ')}
  vec3 ret = vec3(${parsed.expression});
  vec3 previous = texture(previousFrame, clamp(uv, vec2(0.0), vec2(1.0))).rgb;
  outColor = vec4(mix(ret, previous, feedback), outputAlpha);
}`;
};

const normalizeWgslExpression = (expression) =>
  expression
    .replace(/\btexture\s*\(\s*previousFrame\s*,\s*([^)]+)\)/g, 'textureSample(previousFrame, previousSampler, $1)')
    .replace(/\btexture\s*\(\s*shaderTexture([0-3])\s*,\s*([^)]+)\)/g, 'textureSample(shaderTexture$1, shaderTextureSampler, $2)')
    .replace(/\bvec2\s*\(/g, 'vec2f(')
    .replace(/\bvec3\s*\(/g, 'vec3f(')
    .replace(/\bvec4\s*\(/g, 'vec4f(')
    .replace(/\bclamp01\s*\(\s*vec2f\s*\(/g, 'clamp01v2(vec2f(')
    .replace(/\bclamp01\s*\(\s*vec3f\s*\(/g, 'clamp01v3(vec3f(')
    .replace(/\bclamp01\s*\(\s*vec4f\s*\(/g, 'clamp01v4(vec4f(')
    .replace(/\batan\s*\(/g, 'atan2(')
    .replace(/\bmix\s*\(/g, 'mix(')
    .replace(/\bmod\s*\(/g, 'mod(');

const normalizeWgslDeclaration = (declaration) =>
  normalizeWgslExpression(declaration)
    .replace(/^vec2\s+/i, 'var ')
    .replace(/^vec3\s+/i, 'var ')
    .replace(/^vec4\s+/i, 'var ')
    .replace(/^float\s+/i, 'var ')
    .replace(/\s=\s/, ' = ');

export const createTranslatedMilkdropWgslShader = (source) => {
  const parsed = parseShaderProgram(source);
  if (!parsed) return '';
  if (
    [parsed.expression, ...parsed.declarations].some((statement) =>
      /[?&|^~]/.test(statement))
  ) {
    return '';
  }
  const qFields = shaderQRegisterNames.map((name) => `  ${name}: f32,`).join('\n');
  const qLocals = shaderQRegisterNames.map((name) => `  let ${name} = uniforms.${name};`).join('\n');
  const fftFields = Array.from({ length: 64 }, (_unused, index) => `  fft${index}: f32,`).join('\n');
  const waveformFields = Array.from({ length: 64 }, (_unused, index) => `  waveform${index}: f32,`).join('\n');
  const textureDeclarations = parsed.textureSamplers
    .map((_sampler, index) => `@group(0) @binding(${index + 3}) var shaderTexture${index}: texture_2d<f32>;`)
    .join('\n');
  const fftCases = Array.from(
    { length: 63 },
    (_unused, index) => `  if (index == ${index}u) { value = uniforms.fft${index}; }`,
  ).join('\n');
  const waveformCases = Array.from(
    { length: 63 },
    (_unused, index) => `  if (index == ${index}u) { value = uniforms.waveform${index}; }`,
  ).join('\n');
  const declarationSource = parsed.declarations
    .map((declaration) => `  ${normalizeWgslDeclaration(declaration)}`)
    .join('\n');
  const expression = normalizeWgslExpression(parsed.expression);
  return `
struct Uniforms {
  color: vec4f,
  time: f32,
  bass: f32,
  mid: f32,
  treb: f32,
  bass_att: f32,
  mid_att: f32,
  treb_att: f32,
  feedback: f32,
  outputAlpha: f32,
  sampleRate: f32,
  canvasWidth: f32,
  canvasHeight: f32,
${qFields}
${fftFields}
${waveformFields}
};

@group(0) @binding(0) var<uniform> uniforms: Uniforms;
@group(0) @binding(1) var previousFrame: texture_2d<f32>;
@group(0) @binding(2) var previousSampler: sampler;
@group(0) @binding(7) var shaderTextureSampler: sampler;
${textureDeclarations}

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

fn clamp01(value: f32) -> f32 {
  return clamp(value, 0.0, 1.0);
}

fn clamp01v2(value: vec2f) -> vec2f {
  return clamp(value, vec2f(0.0), vec2f(1.0));
}

fn clamp01v3(value: vec3f) -> vec3f {
  return clamp(value, vec3f(0.0), vec3f(1.0));
}

fn clamp01v4(value: vec4f) -> vec4f {
  return clamp(value, vec4f(0.0), vec4f(1.0));
}

fn get_fft(position: f32) -> f32 {
  let index = u32(clamp(position, 0.0, 1.0) * 63.0);
  var value = uniforms.fft63;
${fftCases}
  return value;
}

fn get_fft_hz(hz: f32) -> f32 {
  let nyquist = max(uniforms.sampleRate * 0.5, 1.0);
  return get_fft(hz / nyquist);
}

fn get_waveform(position: f32) -> f32 {
  let index = u32(clamp(position, 0.0, 1.0) * 63.0);
  var value = uniforms.waveform63;
${waveformCases}
  return value;
}

@fragment
fn fragmentMain(input: VertexOutput) -> @location(0) vec4f {
  let uv = input.uv;
  let color = uniforms.color.rgb;
  let time = uniforms.time;
  let bass = uniforms.bass;
  let mid = uniforms.mid;
  let treb = uniforms.treb;
  let bass_att = uniforms.bass_att;
  let mid_att = uniforms.mid_att;
  let treb_att = uniforms.treb_att;
  let x = uv.x;
  let y = uv.y;
  let centeredUv = uv - vec2f(0.5);
  let rad = length(centeredUv);
  let ang = atan2(centeredUv.y, centeredUv.x);
  let resolution = vec2f(max(uniforms.canvasWidth, 1.0), max(uniforms.canvasHeight, 1.0));
  let pixelSize = vec2f(1.0) / resolution;
  let aspect = resolution.x / resolution.y;
  let texsize = vec4f(resolution, pixelSize);
${qLocals}
${declarationSource}
  let ret = vec3f(${expression});
  let previous = textureSample(previousFrame, previousSampler, clamp(uv, vec2f(0.0), vec2f(1.0))).rgb;
  return vec4f(mix(ret, previous, uniforms.feedback), uniforms.outputAlpha);
}`;
};

export const analyzeMilkdropShaderSupport = (source) => ({
  supported: !source || Boolean(createTranslatedMilkdropFragmentShader(source)),
});

export const analyzeMilkdropWebGpuShaderSupport = (source) => ({
  supported: !source || Boolean(createTranslatedMilkdropWgslShader(source)),
});
