import { UsdPipe } from './usd.pipe';

describe('UsdPipe', () => {
  it.each([
    [0, '$0.00'],
    [0.1 + 0.2, '$0.30'],
    [1234567.89, '$1,234,567.89'],
    // Near the cent-exact limit of a double (R-42); .25 is exactly representable.
    [90_000_000_000_000.25, '$90,000,000,000,000.25'],
  ])('formats %d as %s', (amount, expected) => {
    expect(new UsdPipe().transform(amount)).toBe(expected);
  });

  // Same vectors as AdvisoryFormatTests in the backend (D-252): change both together.
  it.each([
    [1562500, '$1,562,500.00'],
    [0.01, '$0.01'],
    [0, '$0.00'],
    [693374.43, '$693,374.43'],
  ])('matches the advisory vector %d -> %s', (amount, expected) => {
    expect(new UsdPipe().transform(amount)).toBe(expected);
  });
});
