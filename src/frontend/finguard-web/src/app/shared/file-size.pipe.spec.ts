import { FileSizePipe } from './file-size.pipe';

describe('FileSizePipe', () => {
  it.each([
    [0, '0 B'],
    [1023, '1023 B'],
    [1024, '1.0 KB'],
    [5 * 1024 * 1024, '5.0 MB'],
  ])('formats %d bytes as %s', (bytes, expected) => {
    expect(new FileSizePipe().transform(bytes)).toBe(expected);
  });
});
