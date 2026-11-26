import { httpResource, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Injector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { problemDetailsInterceptor } from '../core/http/problem-details.interceptor';
import {
  NOT_FOUND_RETRY_DELAYS_MS,
  NotFoundRetry,
  retryWhileNotFound,
} from './retry-while-not-found';

describe('NOT_FOUND_RETRY_DELAYS_MS', () => {
  it('is 15 seconds of retries in four steps (D-205)', () => {
    expect(NOT_FOUND_RETRY_DELAYS_MS).toEqual([1000, 2000, 4000, 8000]);
  });
});

describe('retryWhileNotFound', () => {
  const url = '/api/thing';
  const code = 'Thing.NotFound';
  let backend: HttpTestingController;
  let retry: NotFoundRetry;
  let injector: Injector;

  const settle = async () => {
    await Promise.resolve();
    TestBed.tick();
  };

  const notFound = (withCode = code) =>
    backend
      .expectOne(url)
      .flush(
        { title: 'Not found', status: 404, code: withCode },
        { status: 404, statusText: 'Not Found' },
      );

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    injector = Injector.create({ providers: [], parent: TestBed.inject(Injector) });
    retry = runInInjectionContext(injector, () =>
      retryWhileNotFound(
        httpResource(() => url),
        code,
      ),
    );
    TestBed.tick();
  });

  afterEach(() => {
    backend.verify();
    vi.useRealTimers();
  });

  it('waits each delay before the next request and times out after the last', async () => {
    notFound();
    await settle();
    for (const delay of NOT_FOUND_RETRY_DELAYS_MS) {
      expect(retry.state()).toBe('pending');
      vi.advanceTimersByTime(delay - 1);
      backend.expectNone(url);
      vi.advanceTimersByTime(1);
      await settle();
      notFound();
      await settle();
    }

    expect(retry.state()).toBe('timeout');
    vi.advanceTimersByTime(60_000);
    backend.expectNone(url);
  });

  it('starts over on check again', async () => {
    notFound();
    await settle();
    for (const delay of NOT_FOUND_RETRY_DELAYS_MS) {
      vi.advanceTimersByTime(delay);
      await settle();
      notFound();
      await settle();
    }

    retry.checkAgain();
    await settle();
    backend.expectOne(url).flush({ ok: true });
    await settle();

    expect(retry.state()).toBe('ready');
  });

  it('treats a 404 with another code as an error and stops', async () => {
    notFound('Other.NotFound');
    await settle();

    expect(retry.state()).toBe('error');
    vi.advanceTimersByTime(60_000);
    backend.expectNone(url);
  });
});
