const tokenPattern = /\s*([A-Za-z_][A-Za-z0-9_.]*|\d+\.?\d*|\.\d+|&&|\|\||<<|>>|==|!=|<=|>=|[()+\-*/%,<>&|^!~])/gy;
const assignmentPattern = /^\s*([A-Za-z_][A-Za-z0-9_.]*)\s*(\+=|-=|\*=|\/=|=)\s*(.+?)\s*$/;

const getIndexedSample = (values, position) => {
  if (!values || !values.length) return 0;
  const normalizedPosition = Math.max(0, Math.min(1, Number(position) || 0));
  const index = Math.min(values.length - 1, Math.floor(normalizedPosition * values.length));
  const value = Number(values[index]) || 0;
  return value > 1 ? value / 255 : value;
};

const getFrequencyData = (scope) =>
  scope.frequency_data || scope.frequencies || scope.frequency || scope.spectrum || scope.fft || [];

const getFftHz = (scope, hz) => {
  const frequencyData = getFrequencyData(scope);
  const sampleRate = Number(scope.sample_rate ?? scope.samplerate ?? 44100) || 44100;
  const nyquist = sampleRate / 2;
  return getIndexedSample(frequencyData, nyquist > 0 ? hz / nyquist : 0);
};

const functions = {
  abs: Math.abs,
  above: (left, right) => (left > right ? 1 : 0),
  acos: (value) => Math.acos(Math.max(-1, Math.min(1, value))),
  asin: (value) => Math.asin(Math.max(-1, Math.min(1, value))),
  atan: Math.atan,
  atan2: Math.atan2,
  below: (left, right) => (left < right ? 1 : 0),
  bor: (left, right) => (Math.trunc(left) | Math.trunc(right)),
  band: (left, right) => (Math.trunc(left) & Math.trunc(right)),
  bnot: (value) => ~Math.trunc(value),
  bxor: (left, right) => (Math.trunc(left) ^ Math.trunc(right)),
  ceil: Math.ceil,
  cos: Math.cos,
  div: (left, right) => (right === 0 ? 0 : left / right),
  equal: (left, right) => (Math.abs(left - right) < 0.00001 ? 1 : 0),
  exp: Math.exp,
  floor: Math.floor,
  if: (condition, whenTrue, whenFalse) => (condition !== 0 ? whenTrue : whenFalse),
  int: Math.trunc,
  log: (value) => (value <= 0 ? 0 : Math.log(value)),
  log10: (value) => (value <= 0 ? 0 : Math.log10(value)),
  max: Math.max,
  min: Math.min,
  mod: (left, right) => (right === 0 ? 0 : left % right),
  pow: Math.pow,
  rand: (limit = 1) => {
    const upper = Math.max(0, Math.trunc(limit));
    if (upper <= 0) return 0;
    return Math.floor(Math.random() * upper);
  },
  sign: (value) => (value > 0 ? 1 : value < 0 ? -1 : 0),
  sigmoid: (value, constraint = 1) => {
    const safeConstraint = constraint === 0 ? 1 : constraint;
    return 1 / (1 + Math.exp(-value * safeConstraint));
  },
  sin: Math.sin,
  sqr: (value) => value * value,
  sqrt: (value) => (value < 0 ? 0 : Math.sqrt(value)),
  tan: Math.tan,
};

const callFunction = (name, args, scope) => {
  if (name === 'get_fft') {
    return getIndexedSample(getFrequencyData(scope), args[0]);
  }
  if (name === 'get_fft_hz') {
    return getFftHz(scope, args[0]);
  }
  const fn = functions[name];
  if (!fn) {
    throw new Error(`Unsupported MilkDrop function: ${name}`);
  }
  return fn(...args);
};

export const isMilkdropFunctionSupported = (name) => {
  const normalized = String(name || '').toLowerCase();
  return normalized === 'get_fft' || normalized === 'get_fft_hz' || normalized in functions;
};

const tokenize = (expression) => {
  tokenPattern.lastIndex = 0;
  const tokens = [];
  let match = tokenPattern.exec(expression);
  while (match) {
    tokens.push(match[1]);
    match = tokenPattern.exec(expression);
  }

  const consumed = tokens.join('');
  const normalized = expression.replace(/\s+/g, '');
  if (consumed !== normalized) {
    throw new Error(`Unsupported MilkDrop expression syntax: ${expression}`);
  }

  return tokens;
};

const getVariable = (scope, name) => Number(scope[name] ?? 0);

const constants = {
  e: Math.E,
  pi: Math.PI,
};

const parseExpression = (tokens, scope) => {
  let index = 0;

  const peek = () => tokens[index];
  const consume = () => tokens[index++];
  const match = (...expected) => {
    if (expected.includes(peek())) {
      return consume();
    }
    return null;
  };

  const parsePrimary = () => {
    const token = consume();
    if (token === undefined) {
      throw new Error('Unexpected end of MilkDrop expression.');
    }

    if (token === '(') {
      const value = parseLogicalOr();
      if (!match(')')) {
        throw new Error('Unclosed MilkDrop expression group.');
      }
      return value;
    }

    if (/^[-+]?(?:\d+\.?\d*|\.\d+)$/.test(token)) {
      return Number(token);
    }

    if (/^[A-Za-z_][A-Za-z0-9_.]*$/.test(token)) {
      const name = token.toLowerCase();
      if (match('(')) {
        const args = [];
        if (peek() !== ')') {
          do {
            args.push(parseLogicalOr());
          } while (match(','));
        }
        if (!match(')')) {
          throw new Error(`Unclosed function call: ${token}`);
        }
        return callFunction(name, args, scope);
      }

      return constants[name] ?? getVariable(scope, name);
    }

    throw new Error(`Unexpected MilkDrop token: ${token}`);
  };

  const parseUnary = () => {
    if (match('+')) return parseUnary();
    if (match('-')) return -parseUnary();
    if (match('!')) return parseUnary() === 0 ? 1 : 0;
    if (match('~')) return ~Math.trunc(parseUnary());
    return parsePrimary();
  };

  const parseFactor = () => {
    let value = parseUnary();
    while (['*', '/', '%'].includes(peek())) {
      const operator = consume();
      const right = parseUnary();
      if (operator === '*') value *= right;
      if (operator === '/') value = right === 0 ? 0 : value / right;
      if (operator === '%') value = right === 0 ? 0 : value % right;
    }
    return value;
  };

  const parseTerm = () => {
    let value = parseFactor();
    while (['+', '-'].includes(peek())) {
      const operator = consume();
      const right = parseFactor();
      value = operator === '+' ? value + right : value - right;
    }
    return value;
  };

  const parseShift = () => {
    let value = parseTerm();
    while (['<<', '>>'].includes(peek())) {
      const operator = consume();
      const right = parseTerm();
      value = operator === '<<'
        ? Math.trunc(value) << Math.trunc(right)
        : Math.trunc(value) >> Math.trunc(right);
    }
    return value;
  };

  const parseComparison = () => {
    let value = parseShift();
    while (['<', '>', '<=', '>=', '==', '!='].includes(peek())) {
      const operator = consume();
      const right = parseShift();
      if (operator === '<') value = value < right ? 1 : 0;
      if (operator === '>') value = value > right ? 1 : 0;
      if (operator === '<=') value = value <= right ? 1 : 0;
      if (operator === '>=') value = value >= right ? 1 : 0;
      if (operator === '==') value = Math.abs(value - right) < 0.00001 ? 1 : 0;
      if (operator === '!=') value = Math.abs(value - right) >= 0.00001 ? 1 : 0;
    }
    return value;
  };

  const parseBitwiseAnd = () => {
    let value = parseComparison();
    while (match('&')) {
      value = Math.trunc(value) & Math.trunc(parseComparison());
    }
    return value;
  };

  const parseBitwiseXor = () => {
    let value = parseBitwiseAnd();
    while (match('^')) {
      value = Math.trunc(value) ^ Math.trunc(parseBitwiseAnd());
    }
    return value;
  };

  const parseBitwiseOr = () => {
    let value = parseBitwiseXor();
    while (match('|')) {
      value = Math.trunc(value) | Math.trunc(parseBitwiseXor());
    }
    return value;
  };

  const parseLogicalAnd = () => {
    let value = parseBitwiseOr();
    while (match('&&')) {
      value = value !== 0 && parseBitwiseOr() !== 0 ? 1 : 0;
    }
    return value;
  };

  const parseLogicalOr = () => {
    let value = parseLogicalAnd();
    while (match('||')) {
      value = value !== 0 || parseLogicalAnd() !== 0 ? 1 : 0;
    }
    return value;
  };

  const value = parseLogicalOr();
  if (index < tokens.length) {
    throw new Error(`Unexpected trailing MilkDrop token: ${peek()}`);
  }
  return value;
};

export const evaluateMilkdropExpression = (expression, variables = {}) => {
  const scope = Object.fromEntries(
    Object.entries(variables).map(([key, value]) => [key.toLowerCase(), value]),
  );
  return parseExpression(tokenize(expression), scope);
};

export const evaluateMilkdropEquations = (equations, variables = {}) => {
  const scope = Object.fromEntries(
    Object.entries(variables).map(([key, value]) => [key.toLowerCase(), value]),
  );

  String(equations || '')
    .split(';')
    .map((statement) => statement.trim())
    .filter(Boolean)
    .forEach((statement) => {
      const assignment = assignmentPattern.exec(statement);
      if (!assignment) {
        parseExpression(tokenize(statement), scope);
        return;
      }

      const [, rawName, operator, expression] = assignment;
      const name = rawName.toLowerCase();
      const current = getVariable(scope, name);
      const next = parseExpression(tokenize(expression), scope);
      if (operator === '=') scope[name] = next;
      if (operator === '+=') scope[name] = current + next;
      if (operator === '-=') scope[name] = current - next;
      if (operator === '*=') scope[name] = current * next;
      if (operator === '/=') scope[name] = next === 0 ? 0 : current / next;
    });

  return scope;
};
