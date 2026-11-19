import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, Injector, runInInjectionContext, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthProvider } from '../auth/auth-provider';
import { FakeAuthProvider } from '../auth/fake-auth-provider';
import { ApiError } from '../http/api-error';
import { authInterceptor } from '../http/auth.interceptor';
import { problemDetailsInterceptor } from '../http/problem-details.interceptor';
import { PortfolioApi } from './portfolio-api';

describe('PortfolioApi', () => {
  let api: PortfolioApi;
  let backend: HttpTestingController;

  beforeEach(() => {
    const provider = new FakeAuthProvider();
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
    api = TestBed.inject(PortfolioApi);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  const inContext = <T>(create: () => T): T =>
    runInInjectionContext(TestBed.inject(Injector), create);

  it('lists submissions through the interceptors', async () => {
    const resource = inContext(() => api.submissions());
    TestBed.tick();

    const request = backend.expectOne('/api/portfolio/submissions');
    expect(request.request.method).toBe('GET');
    expect(request.request.headers.get('Authorization')).toBe('Bearer secret-token');
    request.flush({ items: [] });
    await TestBed.inject(ApplicationRef).whenStable();

    expect(resource.value()).toEqual({ items: [] });
  });

  it('reads one submission by id', () => {
    inContext(() => api.submission(signal('abc-123')));
    TestBed.tick();

    backend.expectOne('/api/portfolio/submissions/abc-123').flush({});
  });

  it('exposes the ApiError from the problem details as the resource error', async () => {
    const resource = inContext(() => api.submission(signal('missing')));
    TestBed.tick();

    backend
      .expectOne('/api/portfolio/submissions/missing')
      .flush(
        { title: 'Resource not found', status: 404, code: 'Portfolio.Submission.NotFound' },
        { status: 404, statusText: 'Not Found' },
      );
    await TestBed.inject(ApplicationRef).whenStable();

    expect(resource.error()).toBeInstanceOf(ApiError);
    expect((resource.error() as ApiError).title).toBe('Resource not found');
  });

  it('uploads FormData with file and cutOffDate and no manual Content-Type', async () => {
    const file = new File(['a,b\n'], 'portfolio.csv', { type: 'text/csv' });
    const response = {
      submissionId: 's-1',
      status: 'Received',
      receivedAt: '2026-11-19T10:00:00Z',
    };

    const call = firstValueFrom(api.upload(file, '2026-11-19'));

    const request = backend.expectOne('/api/portfolio/submissions');
    expect(request.request.method).toBe('POST');
    const body = request.request.body as FormData;
    expect(body).toBeInstanceOf(FormData);
    expect((body.get('file') as File).name).toBe('portfolio.csv');
    expect(body.get('cutOffDate')).toBe('2026-11-19');
    expect(request.request.headers.has('Content-Type')).toBe(false);
    request.flush(response);
    await expect(call).resolves.toEqual(response);
  });
});
