import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthSession } from './auth-provider';
import { DEV_SESSION_KEY, DevAuthProvider } from './dev-auth-provider';

describe('DevAuthProvider', () => {
  const future = (): string => new Date(Date.now() + 60 * 60 * 1000).toISOString();
  const past = (): string => new Date(Date.now() - 1000).toISOString();

  let provider: DevAuthProvider;
  let http: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [DevAuthProvider, provideHttpClient(), provideHttpClientTesting()],
    });
    provider = TestBed.inject(DevAuthProvider);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('signs in through /dev/token and stores the session', async () => {
    const session: AuthSession = { accessToken: 'token-a', expiresAt: future() };

    const signIn = provider.signIn('alice');
    const request = http.expectOne('/dev/token');
    request.flush(session);

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ user: 'alice' });
    expect(await signIn).toEqual(session);
    expect(JSON.parse(sessionStorage.getItem(DEV_SESSION_KEY) ?? 'null')).toEqual(session);
  });

  it('restores a stored session that has not expired', () => {
    const session: AuthSession = { accessToken: 'token-a', expiresAt: future() };
    sessionStorage.setItem(DEV_SESSION_KEY, JSON.stringify(session));

    expect(provider.restore()).toEqual(session);
  });

  it('ignores and removes an expired session', () => {
    sessionStorage.setItem(
      DEV_SESSION_KEY,
      JSON.stringify({ accessToken: 'old', expiresAt: past() }),
    );

    expect(provider.restore()).toBeNull();
    expect(sessionStorage.getItem(DEV_SESSION_KEY)).toBeNull();
  });

  it('ignores a corrupt entry', () => {
    sessionStorage.setItem(DEV_SESSION_KEY, '{not json');

    expect(provider.restore()).toBeNull();
  });

  it('clears the stored session on sign-out', () => {
    sessionStorage.setItem(
      DEV_SESSION_KEY,
      JSON.stringify({ accessToken: 'token-a', expiresAt: future() }),
    );

    provider.signOut();

    expect(sessionStorage.getItem(DEV_SESSION_KEY)).toBeNull();
    expect(provider.restore()).toBeNull();
  });

  it('offers the development users', () => {
    expect(provider.users.map((u) => u.id)).toEqual(['alice', 'bob']);
  });
});
