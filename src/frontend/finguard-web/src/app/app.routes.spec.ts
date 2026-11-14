import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';

describe('app routes', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter(routes)] });
  });

  it('renders the home page at the root URL', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/');

    expect(harness.routeNativeElement?.querySelector('h2')?.textContent).toContain('Portfolio compliance');
  });

  it('redirects an unknown URL to the root URL', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/does-not-exist');

    expect(TestBed.inject(Router).url).toBe('/');
    expect(harness.routeNativeElement?.querySelector('h2')?.textContent).toContain('Portfolio compliance');
  });
});
