import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthProvider } from '../auth/auth-provider';
import { AuthService } from '../auth/auth.service';
import { FakeAuthProvider } from '../auth/fake-auth-provider';
import { ApiError } from './api-error';
import { authInterceptor } from './auth.interceptor';
import { problemDetailsInterceptor } from './problem-details.interceptor';

describe('authInterceptor', () => {
  let provider: FakeAuthProvider;
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    provider = new FakeAuthProvider();
    provider.stored = { accessToken: 'secret-token', expiresAt: '2999-01-01T00:00:00Z' };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthProvider, useValue: provider },
        // Same order as app.config.ts.
        provideHttpClient(withInterceptors([authInterceptor, problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  });

  afterEach(() => backend.verify());

  it('adds the bearer token to /api/ calls', () => {
    http.get('/api/v1/tenancy/me').subscribe();

    const request = backend.expectOne('/api/v1/tenancy/me');
    expect(request.request.headers.get('Authorization')).toBe('Bearer secret-token');
    request.flush({});
  });

  it.each(['/dev/token', 'https://example.com/api/tenancy/me', '/apix/other'])(
    'does not add the token to %s',
    (url) => {
      http.get(url).subscribe();

      const request = backend.expectOne(url);
      expect(request.request.headers.has('Authorization')).toBe(false);
      request.flush({});
    },
  );

  it('signs out on a 401 from /api/', async () => {
    const auth = TestBed.inject(AuthService);
    const call = firstValueFrom(http.get('/api/v1/tenancy/me'));

    backend
      .expectOne('/api/v1/tenancy/me')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(call).rejects.toBeInstanceOf(ApiError);
    expect(auth.isAuthenticated()).toBe(false);
    expect(provider.signOutCalls).toBe(1);
  });

  it('keeps the session on other errors', async () => {
    const auth = TestBed.inject(AuthService);
    const call = firstValueFrom(http.get('/api/v1/tenancy/me'));

    backend.expectOne('/api/v1/tenancy/me').flush(null, { status: 403, statusText: 'Forbidden' });

    await expect(call).rejects.toBeInstanceOf(ApiError);
    expect(auth.isAuthenticated()).toBe(true);
  });
});
