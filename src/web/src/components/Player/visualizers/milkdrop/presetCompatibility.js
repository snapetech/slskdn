import { isMilkdropFunctionSupported } from './expressionVm';
import { analyzeMilkdropShaderSupport } from './shaderTranslator';

const functionCallPattern = /\b([A-Za-z_][A-Za-z0-9_.]*)\s*\(/g;

const collectFunctions = (text, functions) => {
  let match = functionCallPattern.exec(text || '');
  while (match) {
    const name = match[1].toLowerCase();
    if (!isMilkdropFunctionSupported(name)) {
      functions.add(name);
    }
    match = functionCallPattern.exec(text || '');
  }
};

const collectEquationFunctions = (equations = {}, functions) => {
  Object.values(equations).forEach((equationText) => collectFunctions(equationText, functions));
};

export const analyzeMilkdropPresetCompatibility = (preset = {}) => {
  const unsupportedFunctions = new Set();
  const shaderSections = [];

  collectEquationFunctions(preset.equations, unsupportedFunctions);
  (preset.shapes || []).forEach((shape) =>
    collectEquationFunctions(shape.equations, unsupportedFunctions));
  (preset.sprites || []).forEach((sprite) =>
    collectEquationFunctions(sprite.equations, unsupportedFunctions));
  (preset.waves || []).forEach((wave) =>
    collectEquationFunctions(wave.equations, unsupportedFunctions));

  if (preset.shaders?.warp && !analyzeMilkdropShaderSupport(preset.shaders.warp).supported) {
    shaderSections.push('warp_shader');
  }
  if (preset.shaders?.comp && !analyzeMilkdropShaderSupport(preset.shaders.comp).supported) {
    shaderSections.push('comp_shader');
  }

  return {
    shaderSections,
    unsupportedFunctions: Array.from(unsupportedFunctions).sort(),
  };
};

export const getMilkdropCompatibilityError = (report) => {
  const messages = [];
  if (report.unsupportedFunctions.length > 0) {
    messages.push(`unsupported functions: ${report.unsupportedFunctions.join(', ')}`);
  }
  if (report.shaderSections.length > 0) {
    messages.push(`shader translation pending: ${report.shaderSections.join(', ')}`);
  }
  return messages.length > 0
    ? `Native MilkDrop preset has ${messages.join('; ')}.`
    : '';
};
