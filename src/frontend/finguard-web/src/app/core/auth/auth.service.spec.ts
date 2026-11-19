import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthProvider } from './auth-provider';
import { AuthService } from './auth.service';
import { FakeAuthProvider } from './fake-auth-provider';

describe('AuthService', () => {
  let provider: FakeAuthProvider;

  function create(): AuthService {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthProvider, useValue: provider }],
    });
    return TestBed.inject(AuthService);
  }

  beforeEach(() => {
    provider = new FakeAuthProvider();
  });

  it('starts signed out when the provider has no session', () => {
    const auth = create();

    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.accessToken()).toBeNull();
  });

  it('starts signed in when the provider restores a session', () => {
    provider.stored = { accessToken: 'restored', expiresAt: '2999-01-01T00:00:00Z' };

    const auth = create();

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.accessToken()).toBe('restored');
  });

  it('follows the session after sign-in', async () => {
    const auth = create();

    await auth.signIn('bob');

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.accessToken()).toBe('token-bob');
  });

  it('clears the session and navigates to /login on sign-out', async () => {
    const auth = create();
    await auth.signIn('alice');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    auth.signOut();

    expect(provider.signOutCalls).toBe(1);
    expect(auth.isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledWith('/login');
  });
});
