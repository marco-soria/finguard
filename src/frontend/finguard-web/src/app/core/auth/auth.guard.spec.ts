import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { AuthProvider } from './auth-provider';
import { authGuard } from './auth.guard';
import { FakeAuthProvider } from './fake-auth-provider';

describe('authGuard', () => {
  let provider: FakeAuthProvider;

  beforeEach(() => {
    provider = new FakeAuthProvider();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthProvider, useValue: provider }],
    });
  });

  const run = () =>
    TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );

  it('redirects to /login when signed out', () => {
    const result = run();

    expect(result).toBeInstanceOf(UrlTree);
    expect(String(result)).toBe('/login');
  });

  it('allows navigation when signed in', () => {
    provider.stored = { accessToken: 't', expiresAt: '2999-01-01T00:00:00Z' };

    expect(run()).toBe(true);
  });
});
