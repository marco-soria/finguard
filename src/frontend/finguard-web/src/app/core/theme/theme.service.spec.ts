import { TestBed } from '@angular/core/testing';
import { THEME_STORAGE_KEY, ThemeService } from './theme.service';

/** A `matchMedia` stand-in whose dark preference the test can flip, firing `change` like the browser. */
function fakeOs(dark: boolean) {
  const listeners: ((event: { matches: boolean }) => void)[] = [];
  const media = {
    matches: dark,
    addEventListener: (_: 'change', listener: (event: { matches: boolean }) => void) =>
      listeners.push(listener),
  };
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    value: (query: string) => (query === '(prefers-color-scheme: dark)' ? media : undefined),
  });
  return {
    set(value: boolean) {
      media.matches = value;
      listeners.forEach((listener) => listener({ matches: value }));
    },
  };
}

describe('ThemeService', () => {
  const create = () => TestBed.inject(ThemeService);

  beforeEach(() => localStorage.clear());

  afterEach(() => {
    vi.restoreAllMocks();
    Reflect.deleteProperty(window, 'matchMedia');
    document.documentElement.style.colorScheme = '';
  });

  it('follows a dark operating system when nothing is stored', () => {
    fakeOs(true);

    const service = create();
    service.apply();

    expect(service.effective()).toBe('dark');
    expect(document.documentElement.style.colorScheme).toBe('light dark');
  });

  it('follows a light operating system when nothing is stored', () => {
    fakeOs(false);

    expect(create().effective()).toBe('light');
  });

  it('updates the effective theme when the operating system changes', () => {
    const os = fakeOs(false);
    const service = create();

    os.set(true);

    expect(service.effective()).toBe('dark');
  });

  it('toggles to the opposite of the effective theme and stores it', () => {
    fakeOs(true);
    const service = create();

    service.toggle();

    expect(service.effective()).toBe('light');
    expect(document.documentElement.style.colorScheme).toBe('light');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light');

    service.toggle();

    expect(service.effective()).toBe('dark');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
  });

  it('keeps a stored choice over the operating system', () => {
    const os = fakeOs(true);
    localStorage.setItem(THEME_STORAGE_KEY, 'light');

    const service = create();
    service.apply();
    os.set(true);

    expect(service.effective()).toBe('light');
    expect(document.documentElement.style.colorScheme).toBe('light');
  });

  it.each(['system', 'purple'])('treats a stored %s as no choice', (stored) => {
    fakeOs(true);
    localStorage.setItem(THEME_STORAGE_KEY, stored);

    const service = create();
    service.apply();

    expect(service.effective()).toBe('dark');
    expect(document.documentElement.style.colorScheme).toBe('light dark');
  });

  it('works without matchMedia', () => {
    expect(create().effective()).toBe('light');
  });

  it('keeps working when storage throws', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });

    const service = create();
    service.toggle();

    expect(service.effective()).toBe('dark');
    expect(document.documentElement.style.colorScheme).toBe('dark');
  });
});
