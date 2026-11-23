import { formatPercent, formatRatio } from './format-ratio';

describe('formatRatio', () => {
  it.each([
    [0.89999996, 'Compliant', '89.999996 %'],
    [0.9, 'Warning', '90.00 %'],
    [1, 'Warning', '100.00 %'],
    [1.00000004, 'Breached', '100.000004 %'],
    [1.0001, 'Breached', '100.01 %'],
    [0.5, 'Compliant', '50.00 %'],
  ])('formats %d %s as %s', (value, status, expected) => {
    expect(formatRatio(value, status, 0.9)).toBe(expected);
  });

  it('never shows 90.00 % next to Compliant', () => {
    expect(formatRatio(0.89999996, 'Compliant', 0.9)).not.toBe('90.00 %');
  });

  it('returns the 8-decimal text when even 8 decimals cannot agree with the status', () => {
    // 89.999999996 % rounds to 90.00000000 at 8 decimals: the cap wins over agreement (documented limit).
    expect(formatRatio(0.89999999996, 'Compliant', 0.9)).toBe('90.00000000 %');
  });

  it('uses the threshold it is given', () => {
    expect(formatRatio(0.79999, 'Compliant', 0.8)).toBe('79.999 %');
  });
});

describe('formatPercent', () => {
  it('uses 2 decimals', () => {
    expect(formatPercent(0.25)).toBe('25.00 %');
    expect(formatPercent(2.2 / 3)).toBe('73.33 %');
  });
});
