import { DOCUMENT } from '@angular/common';
import { inject, Injectable, signal } from '@angular/core';

export type Theme = 'system' | 'light' | 'dark';

export const THEME_STORAGE_KEY = 'finguard.theme';

const COLOR_SCHEME: Record<Theme, string> = {
  system: 'light dark',
  light: 'light',
  dark: 'dark',
};

const isTheme = (value: unknown): value is Theme =>
  value === 'system' || value === 'light' || value === 'dark';

/**
 * Owns `color-scheme` on <html> (D-210). The `--mat-sys-*` and `--fg-*` tokens are `light-dark()` values, so this one
 * property switches the whole app. The choice is a browser preference, kept in `localStorage`.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly current = signal<Theme>(this.read());

  readonly theme = this.current.asReadonly();

  /** Called once at startup (`provideAppInitializer`). */
  apply(): void {
    this.document.documentElement.style.colorScheme = COLOR_SCHEME[this.current()];
  }

  set(theme: Theme): void {
    this.current.set(theme);
    this.apply();
    try {
      localStorage.setItem(THEME_STORAGE_KEY, theme);
    } catch {
      // Storage blocked (private mode, policy): the choice lasts for this page only.
    }
  }

  private read(): Theme {
    try {
      const stored = localStorage.getItem(THEME_STORAGE_KEY);
      return isTheme(stored) ? stored : 'system';
    } catch {
      return 'system';
    }
  }
}
