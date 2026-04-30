const unsupportedPatterns = [
  /\bfor\s*\(/i,
  /\bwhile\s*\(/i,
  /\bif\s*\(/i,
  /\bfloat[234]x[234]\b/i,
  /\bmul\s*\(/i,
  /\bsampler\b/i,
];

const allowedExpressionPattern = /^[A-Za-z0-9_.,+\-*/%<>=!&|^~?:()\s]+$/;
const declarationPattern = /^(float|float2|float3|float4|vec2|vec3|vec4)\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+)$/i;
const shaderQRegisterNames = Array.from({ length: 64 }, (_unused, index) => `q${index + 1}`);
const shaderAudioVariableNames = ['bass', 'bass_att', 'mid', 'mid_att', 'treb', 'treb_att'];
const shaderVariableNames = [...shaderAudioVariableNames, ...shaderQRegisterNames];

const stripShaderComments = (source) =>
  String(source || '')
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/\/\/.*$/gm, '')
    .trim();

const normalizeShaderSource = (source) =>
  stripShaderComments(source)
    .replace(/\btex2D\s*\(\s*sampler_(?:main|fc_main|sampler_main)\s*,/gi, 'texture(previousFrame,')
    .replace(/\btex2D\s*\(\s*(?:sampler_main|previousFrame)\s*,/gi, 'texture(previousFrame,')
    .replace(/\btex\s*\(\s*(?:sampler_main|previousFrame)\s*,/gi, 'texture(previousFrame,')
    .replace(/\btexsize\b/gi, 'textureSize');

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
  return !(/\btexture\s*\(/i.test(expression) && !/\bpreviousFrame\b/.test(expression));
};

const parseShaderProgram = (source) => {
  if (unsupportedPatterns.some((pattern) => pattern.test(source))) return null;
  const cleaned = normalizeShaderSource(source);
  const statements = cleaned
    .split(';')
    .map((statement) => statement.trim())
    .filter(Boolean);
  const declarations = [];
  let expression = '';

  for (const statement of statements) {
    const retMatch = /^ret\s*=\s*(.+)$/i.exec(statement);
    if (retMatch) {
      expression = normalizeShaderExpression(retMatch[1].trim());
      continue;
    }

    const declarationMatch = declarationPattern.exec(statement);
    if (declarationMatch) {
      const declarationExpression = normalizeShaderExpression(declarationMatch[3].trim());
      if (!isSafeShaderExpression(declarationExpression)) return null;
      declarations.push(
        `${normalizeShaderType(declarationMatch[1])} ${declarationMatch[2]} = ${declarationExpression};`,
      );
      continue;
    }

    return null;
  }

  if (!isSafeShaderExpression(expression)) return null;
  return {
    declarations,
    expression,
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
  return `#version 300 es
precision highp float;
uniform vec3 color;
uniform sampler2D previousFrame;
uniform float feedback;
uniform float outputAlpha;
uniform float time;
uniform float sampleRate;
uniform float fftBins[32];
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
  int index = int(clamp(position, 0.0, 1.0) * 31.0);
  return fftBins[index];
}
float get_fft_hz(float hz) {
  float nyquist = max(sampleRate * 0.5, 1.0);
  return get_fft(hz / nyquist);
}
void main() {
  ${parsed.declarations.join('\n  ')}
  vec3 ret = vec3(${parsed.expression});
  vec3 previous = texture(previousFrame, clamp(uv, vec2(0.0), vec2(1.0))).rgb;
  outColor = vec4(mix(ret, previous, feedback), outputAlpha);
}`;
};

export const analyzeMilkdropShaderSupport = (source) => ({
  supported: !source || Boolean(createTranslatedMilkdropFragmentShader(source)),
});
