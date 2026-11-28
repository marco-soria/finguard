import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatTooltip } from '@angular/material/tooltip';
import { By } from '@angular/platform-browser';
import { provideRouter, Router } from '@angular/router';
import { Observable, of, Subject, throwError } from 'rxjs';
import { App } from './app';
import { CurrentTenant, TenancyApi } from './core/api/tenancy-api';
import { AuthProvider } from './core/auth/auth-provider';
import { AuthService } from './core/auth/auth.service';
import { FakeAuthProvider } from './core/auth/fake-auth-provider';
import { ApiError } from './core/http/api-error';
import { registerThemeIcons } from './core/theme/theme-icons';
import { THEME_STORAGE_KEY } from './core/theme/theme.service';

describe('App', () => {
  let provider: FakeAuthProvider;
  let tenant$: Observable<CurrentTenant>;

  beforeEach(() => {
    provider = new FakeAuthProvider();
    tenant$ = of({ tenantId: 't-1', tenantName: 'Acme Pension Fund' });
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        { provide: AuthProvider, useValue: provider },
        { provide: TenancyApi, useValue: { getCurrentTenant: () => tenant$ } },
      ],
    });
  });

  const signOutButton = (element: HTMLElement) =>
    Array.from(element.querySelectorAll<HTMLButtonElement>('header button')).find(
      (b) => b.textContent?.trim() === 'Sign out',
    );

  function render(signedIn: boolean): HTMLElement {
    if (signedIn) {
      provider.stored = { accessToken: 't', expiresAt: '2999-01-01T00:00:00Z' };
    }
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders a header with the FinGuard title and a router outlet', () => {
    const element = render(false);

    expect(element.querySelector('header h1')?.textContent).toContain('FinGuard');
    expect(element.querySelector('main router-outlet')).not.toBeNull();
  });

  it('shows no tenant and no sign-out button when signed out', () => {
    const element = render(false);

    expect(element.querySelector('.app-tenant')).toBeNull();
    expect(signOutButton(element)).toBeUndefined();
  });

  it('shows the tenant name when signed in', () => {
    const element = render(true);

    expect(element.querySelector('.app-tenant')?.textContent).toContain('Acme Pension Fund');
  });

  it('shows nothing while the tenant is loading', () => {
    tenant$ = new Subject<CurrentTenant>();

    const element = render(true);

    expect(element.querySelector('.app-tenant')).toBeNull();
  });

  it('shows a neutral message when the tenant cannot be loaded', () => {
    tenant$ = throwError(() => new ApiError(500, 'Server error'));

    const element = render(true);

    expect(element.querySelector('.app-tenant')?.textContent).toContain(
      'Could not load your organization.',
    );
  });

  it('signs out through the auth service', () => {
    vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const element = render(true);
    const signOut = vi.spyOn(TestBed.inject(AuthService), 'signOut');

    signOutButton(element)?.click();

    expect(signOut).toHaveBeenCalledOnce();
  });

  const themeButton = (fixture: ComponentFixture<App>) =>
    fixture.debugElement.query(By.directive(MatTooltip));

  function renderTheme(): ComponentFixture<App> {
    TestBed.runInInjectionContext(registerThemeIcons);
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    return fixture;
  }

  describe('theme toggle', () => {
    beforeEach(() => localStorage.clear());

    afterEach(() => {
      localStorage.clear();
      document.documentElement.style.colorScheme = '';
    });

    it('offers dark in light mode, with a label and a tooltip, also when signed out', () => {
      const fixture = renderTheme();
      const button = themeButton(fixture);

      expect(button.attributes['aria-label']).toBe('Switch to dark theme');
      expect(button.injector.get(MatTooltip).message).toBe('Switch to dark theme');
      expect(button.query(By.css('mat-icon')).attributes['data-mat-icon-name']).toBe('dark_mode');
    });

    it('switches to dark on click and then offers light', () => {
      const fixture = renderTheme();

      (themeButton(fixture).nativeElement as HTMLButtonElement).click();
      fixture.detectChanges();

      const button = themeButton(fixture);
      expect(button.attributes['aria-label']).toBe('Switch to light theme');
      expect(button.injector.get(MatTooltip).message).toBe('Switch to light theme');
      expect(button.query(By.css('mat-icon')).attributes['data-mat-icon-name']).toBe('light_mode');
      expect(document.documentElement.style.colorScheme).toBe('dark');
      expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
    });
  });
});
