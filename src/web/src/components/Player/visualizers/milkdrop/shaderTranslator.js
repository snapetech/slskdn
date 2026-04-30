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

export const analyzeMilkdropShaderSupport = (source) => ({
  supported: !source || Boolean(createTranslatedMilkdropFragmentShader(source)),
});
