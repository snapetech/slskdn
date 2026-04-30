import {
  evaluateMilkdropEquations,
  evaluateMilkdropExpression,
} from './expressionVm';

describe('MilkDrop expression VM', () => {
  it('evaluates arithmetic, variables, and core helper functions', () => {
    expect(evaluateMilkdropExpression('pow(bass_att, 2) + sqr(3)', {
      bass_att: 2,
    })).toBe(13);
    expect(evaluateMilkdropExpression('if(above(treb, 1.5), sin(0), 7)', {
      treb: 2,
    })).toBe(0);
    expect(evaluateMilkdropExpression('div(10, 0) + sqrt(-1)')).toBe(0);
    expect(evaluateMilkdropExpression('q33 >= 2', { q33: 2 })).toBe(1);
  });

  it('runs assignment statements in order and preserves q1-q64 variables', () => {
    const scope = evaluateMilkdropEquations(
      `
        q1 = bass_att * 0.2;
        zoom += q1;
        q33 = if(below(treb_att, 2), 7, 9);
        wave_r *= 0.5;
      `,
      {
        bass_att: 3,
        treb_att: 1,
        wave_r: 0.8,
        zoom: 1,
      },
    );

    expect(scope.q1).toBeCloseTo(0.6);
    expect(scope.zoom).toBeCloseTo(1.6);
    expect(scope.q33).toBe(7);
    expect(scope.wave_r).toBeCloseTo(0.4);
  });

  it('evaluates analyzer-backed FFT helper functions', () => {
    expect(evaluateMilkdropExpression('get_fft(0.5)', {
      frequency_data: [0, 128, 255, 64],
    })).toBeCloseTo(1);
    expect(evaluateMilkdropExpression('get_fft_hz(5512.5)', {
      frequency_data: [0, 255, 0, 0],
      sample_rate: 44100,
    })).toBeCloseTo(1);
    const scope = evaluateMilkdropEquations(
      'q1=get_fft(0.25); q2=get_fft_hz(22050);',
      {
        frequency_data: [0, 255, 128, 64],
        sample_rate: 44100,
      },
    );
    expect(scope.q1).toBeCloseTo(1);
    expect(scope.q2).toBeCloseTo(64 / 255);
  });

  it('evaluates common NSEEL compatibility helpers and constants', () => {
    expect(evaluateMilkdropExpression('sin(pi/2)+log(e)+log10(100)')).toBeCloseTo(4);
    expect(evaluateMilkdropExpression('atan2(1, 0)')).toBeCloseTo(Math.PI / 2);
    expect(evaluateMilkdropExpression('asin(2)+acos(-2)')).toBeCloseTo(Math.PI * 1.5);
    expect(evaluateMilkdropExpression('band(7, 3)+bor(4, 1)+bxor(7, 3)')).toBe(12);
    expect(evaluateMilkdropExpression('bnot(0)')).toBe(-1);
    expect(evaluateMilkdropExpression('sign(-3)+sign(0)+sign(4)')).toBe(0);
    expect(evaluateMilkdropExpression('sigmoid(0, 2)')).toBeCloseTo(0.5);
    expect(evaluateMilkdropExpression('tan(0)+exp(0)')).toBeCloseTo(1);
  });

  it('evaluates inline bitwise, shift, unary, and logical operators used by presets', () => {
    expect(evaluateMilkdropExpression('(7 & 3) + (4 | 1) + (7 ^ 3)')).toBe(12);
    expect(evaluateMilkdropExpression('(1 << 3) + (8 >> 1)')).toBe(12);
    expect(evaluateMilkdropExpression('~0 + !0 + !2')).toBe(0);
    expect(evaluateMilkdropExpression('(q1 > 1) && (q2 < 4)', {
      q1: 2,
      q2: 3,
    })).toBe(1);
    expect(evaluateMilkdropExpression('(q1 > 1) || (q2 < 1)', {
      q1: 0,
      q2: 3,
    })).toBe(0);
    expect(evaluateMilkdropExpression('if(q1 & 3, 9, 4)', { q1: 2 })).toBe(9);
  });

  it('evaluates rand within MilkDrop integer bounds', () => {
    const values = Array.from({ length: 20 }, () =>
      evaluateMilkdropExpression('rand(4)'));

    values.forEach((value) => {
      expect(Number.isInteger(value)).toBe(true);
      expect(value).toBeGreaterThanOrEqual(0);
      expect(value).toBeLessThan(4);
    });
    expect(evaluateMilkdropExpression('rand(0)')).toBe(0);
  });

  it('throws on unsupported syntax instead of silently mis-evaluating presets', () => {
    expect(() => evaluateMilkdropExpression('megabuf(1)')).toThrow(
      'Unsupported MilkDrop function',
    );
    expect(() => evaluateMilkdropExpression('q1 @ 3')).toThrow(
      'Unsupported MilkDrop expression syntax',
    );
  });
});
