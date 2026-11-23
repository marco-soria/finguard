import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { Observable, of, Subject, throwError } from 'rxjs';
import { App } from './app';
import { CurrentTenant, TenancyApi } from './core/api/tenancy-api';
import { AuthProvider } from './core/auth/auth-provider';
import { AuthService } from './core/auth/auth.service';
import { FakeAuthProvider } from './core/auth/fake-auth-provider';
import { ApiError } from './core/http/api-error';
import { ThemeService } from './core/theme/theme.service';

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

  it('offers a theme toggle with an accessible name, also when signed out', () => {
    const element = render(false);

    const group = element.querySelector('header mat-button-toggle-group');
    expect(group?.getAttribute('aria-label')).toBe('Theme');
    expect(
      Array.from(group?.querySelectorAll('button') ?? []).map((b) => b.textContent?.trim()),
    ).toEqual(['System', 'Light', 'Dark']);
  });

  it('changes the theme from the toggle', () => {
    localStorage.clear();
    const element = render(false);
    const dark = Array.from(element.querySelectorAll<HTMLButtonElement>('header button')).find(
      (b) => b.textContent?.trim() === 'Dark',
    );

    dark?.click();

    expect(TestBed.inject(ThemeService).theme()).toBe('dark');
    expect(document.documentElement.style.colorScheme).toBe('dark');
    localStorage.clear();
    document.documentElement.style.colorScheme = '';
  });
});
