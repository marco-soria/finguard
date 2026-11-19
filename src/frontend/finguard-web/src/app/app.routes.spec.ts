import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { AuthProvider } from './core/auth/auth-provider';
import { FakeAuthProvider } from './core/auth/fake-auth-provider';

describe('app routes', () => {
  let provider: FakeAuthProvider;

  beforeEach(() => {
    provider = new FakeAuthProvider();
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        { provide: AuthProvider, useValue: provider },
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
  });

  const heading = (harness: RouterTestingHarness) =>
    harness.routeNativeElement?.querySelector('h2')?.textContent;

  it('redirects to /login when signed out', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/');

    expect(TestBed.inject(Router).url).toBe('/login');
    expect(heading(harness)).toContain('Sign in (development)');
  });

  it('renders the submissions page at the root URL when signed in', async () => {
    provider.stored = { accessToken: 't', expiresAt: '2999-01-01T00:00:00Z' };
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/');

    expect(heading(harness)).toContain('Upload a portfolio');
  });

  it('renders the submission detail page when signed in', async () => {
    provider.stored = { accessToken: 't', expiresAt: '2999-01-01T00:00:00Z' };
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/submissions/s-1');

    expect(heading(harness)).toContain('Submission');
  });

  it('redirects /submissions/:id to /login when signed out', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/submissions/s-1');

    expect(TestBed.inject(Router).url).toBe('/login');
  });

  it('redirects an unknown URL to the root URL when signed in', async () => {
    provider.stored = { accessToken: 't', expiresAt: '2999-01-01T00:00:00Z' };
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/does-not-exist');

    expect(TestBed.inject(Router).url).toBe('/');
  });

  it('sends an unknown URL to /login when signed out', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/does-not-exist');

    expect(TestBed.inject(Router).url).toBe('/login');
  });
});
