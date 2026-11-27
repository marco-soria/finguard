import { API_BASE } from './api-base';

describe('API_BASE', () => {
  it('is the v1 prefix under /api/, so the auth interceptor and the proxy still match it', () => {
    expect(API_BASE).toBe('/api/v1');
    expect(API_BASE.startsWith('/api/')).toBe(true);
  });
});
