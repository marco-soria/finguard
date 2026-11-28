import { DOCUMENT } from '@angular/common';
import { computed, inject, Injectable, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

export const THEME_STORAGE_KEY = 'finguard.theme';

const DARK_QUERY = '(prefers-color-scheme: dark)';

// A stored `system` (the three-button selector of #15) or anything unknown counts as no choice (D-319).
const isTheme = (value: unknown): value is Theme => value === 'light' || value === 'dark';

/**
 * Owns `color-scheme` on <html> (D-210). The `--mat-sys-*` and `--fg-*` tokens are `light-dark()` values, so this one
 * property switches the whole app. Without a stored choice it is `light dark` and the browser follows the operating
 * system live; a toggle stores `light` or `dark` in `localStorage` (D-319). There is no way back to "follow the system"
 * other than clearing the storage (accepted limitation).
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly stored = signal<Theme | null>(this.read());
  private readonly osDark = signal(false);

  /** The theme on screen: the stored choice, otherwise the operating system's. */
  readonly effective = computed<Theme>(() => this.stored() ?? (this.osDark() ? 'dark' : 'light'));

  constructor() {
    const media = this.document.defaultView?.matchMedia?.(DARK_QUERY);
    if (media) {
      this.osDark.set(media.matches);
      media.addEventListener('change', (event) => this.osDark.set(event.matches));
    }
  }

  /** Called once at startup (`provideAppInitializer`). */
  apply(): void {
    this.document.documentElement.style.colorScheme = this.stored() ?? 'light dark';
  }

  /** Switches to the opposite of what is on screen and stores it. */
  toggle(): void {
    const theme: Theme = this.effective() === 'dark' ? 'light' : 'dark';
    this.stored.set(theme);
    this.apply();
    try {
      localStorage.setItem(THEME_STORAGE_KEY, theme);
    } catch {
      // Storage blocked (private mode, policy): the choice lasts for this page only.
    }
  }

  private read(): Theme | null {
    try {
      const stored = localStorage.getItem(THEME_STORAGE_KEY);
      return isTheme(stored) ? stored : null;
    } catch {
      return null;
    }
  }
}
