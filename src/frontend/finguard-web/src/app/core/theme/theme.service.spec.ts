import { TestBed } from '@angular/core/testing';
import { THEME_STORAGE_KEY, ThemeService } from './theme.service';

describe('ThemeService', () => {
  const create = () => TestBed.inject(ThemeService);

  beforeEach(() => localStorage.clear());

  afterEach(() => {
    vi.restoreAllMocks();
    document.documentElement.style.colorScheme = '';
  });

  it('defaults to system', () => {
    const service = create();
    service.apply();

    expect(service.theme()).toBe('system');
    expect(document.documentElement.style.colorScheme).toBe('light dark');
  });

  it('applies and stores the chosen theme', () => {
    const service = create();

    service.set('dark');

    expect(service.theme()).toBe('dark');
    expect(document.documentElement.style.colorScheme).toBe('dark');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
  });

  it('reads the stored theme', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'light');

    const service = create();
    service.apply();

    expect(service.theme()).toBe('light');
    expect(document.documentElement.style.colorScheme).toBe('light');
  });

  it('ignores an unknown stored value', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'purple');

    expect(create().theme()).toBe('system');
  });

  it('keeps working when storage throws', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });

    const service = create();
    service.set('dark');

    expect(service.theme()).toBe('dark');
    expect(document.documentElement.style.colorScheme).toBe('dark');
  });
});
