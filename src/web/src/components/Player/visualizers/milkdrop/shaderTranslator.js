const unsupportedPatterns = [
  /\bfor\s*\(/i,
  /\bwhile\s*\(/i,
  /\bif\s*\(/i,
  /\bfloat[234]x[234]\b/i,
  /\bmul\s*\(/i,
  /\bsampler\b/i,
];

const allowedExpressionPattern = /^[A-Za-z0-9_.,+\-*/%<>=!&|^~?:()\s]+$/;
const shaderQRegisterNames = Array.from({ length: 64 }, (_unused, index) => `q${index + 1}`);
const shaderAudioVariableNames = ['bass', 'bass_att', 'mid', 'mid_att', 'treb', 'treb_att'];
const shaderVariableNames = [...shaderAudioVariableNames, ...shaderQRegisterNames];

const stripShaderComments = (source) =>
  String(source || '')
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/\/\/.*$/gm, '')
    .trim();

const extractReturnExpression = (source) => {
  const cleaned = stripShaderComments(source)
    .replace(/\btex2D\s*\(\s*sampler_(?:main|fc_main|sampler_main)\s*,/gi, 'texture(previousFrame,')
    .replace(/\btex2D\s*\(\s*(?:sampler_main|previousFrame)\s*,/gi, 'texture(previousFrame,')
    .replace(/\btex\s*\(\s*(?:sampler_main|previousFrame)\s*,/gi, 'texture(previousFrame,')
    .replace(/\btexsize\b/gi, 'textureSize');
  const retMatch = /\bret\s*=\s*([^;]+)\s*;?/i.exec(cleaned);
  if (!retMatch) return '';
  return retMatch[1].trim();
};

export const translateMilkdropShaderExpression = (source) => {
  const expression = extractReturnExpression(source);
  if (!expression) return '';
  if (unsupportedPatterns.some((pattern) => pattern.test(source))) return '';
  if (!allowedExpressionPattern.test(expression)) return '';
  if (/\btexture\s*\(/i.test(expression) && !/\bpreviousFrame\b/.test(expression)) return '';
  return expression
    .replace(/\bfloat4\s*\(/gi, 'vec4(')
    .replace(/\bfloat3\s*\(/gi, 'vec3(')
    .replace(/\bfloat2\s*\(/gi, 'vec2(')
    .replace(/\bsaturate\s*\(/gi, 'clamp01(')
    .replace(/\blerp\s*\(/gi, 'mix(');
};

export const createTranslatedMilkdropFragmentShader = (source) => {
  const expression = translateMilkdropShaderExpression(source);
  if (!expression) return '';
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
void main() {
  vec3 ret = vec3(${expression});
  vec3 previous = texture(previousFrame, clamp(uv, vec2(0.0), vec2(1.0))).rgb;
  outColor = vec4(mix(ret, previous, feedback), outputAlpha);
}`;
};

export const analyzeMilkdropShaderSupport = (source) => ({
  supported: !source || Boolean(createTranslatedMilkdropFragmentShader(source)),
});
