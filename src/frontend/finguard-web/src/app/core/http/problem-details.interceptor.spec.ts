import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { ApiError } from './api-error';
import {
  errorMessage,
  GENERIC_ERROR_TITLE,
  problemDetailsInterceptor,
} from './problem-details.interceptor';

describe('problemDetailsInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  async function failWith(
    body: object | string | null,
    status: number,
    headers?: Record<string, string>,
  ): Promise<unknown> {
    const call = firstValueFrom(http.get('/api/x'));
    backend.expectOne('/api/x').flush(body, { status, statusText: 'Error', headers });
    return call.then(
      () => null,
      (error: unknown) => error,
    );
  }

  it('maps ProblemDetails with detail, code and traceId', async () => {
    const error = await failWith(
      {
        type: 'https://tools.ietf.org/html/rfc9110#section-15.5.4',
        title: 'Forbidden',
        status: 403,
        detail: 'The signed-in user is not provisioned to a tenant.',
        code: 'Tenancy.User.NotProvisioned',
        traceId: '00-abc-def-01',
      },
      403,
    );

    expect(error).toEqual(
      new ApiError(
        403,
        'Forbidden',
        'The signed-in user is not provisioned to a tenant.',
        'Tenancy.User.NotProvisioned',
        '00-abc-def-01',
      ),
    );
  });

  it('maps ProblemDetails without code or traceId', async () => {
    const error = await failWith({ title: 'Resource not found', status: 404 }, 404);

    expect(error).toEqual(new ApiError(404, 'Resource not found'));
  });

  it('ignores detail and code that are not strings', async () => {
    const error = await failWith({ title: 'Validation failed', detail: 42, code: { x: 1 } }, 400);

    expect(error).toEqual(new ApiError(400, 'Validation failed'));
  });

  it('maps a non-ProblemDetails error to a generic ApiError with the status', async () => {
    const error = await failWith('<html>Bad gateway: upstream secret detail</html>', 502);

    expect(error).toEqual(new ApiError(502, GENERIC_ERROR_TITLE));
  });

  it('maps a 429 with Retry-After to its seconds', async () => {
    const error = await failWith(
      { title: 'Too many requests', status: 429, code: 'RateLimit.Exceeded' },
      429,
      { 'Retry-After': '42' },
    );

    expect(error).toEqual(
      new ApiError(429, 'Too many requests', undefined, 'RateLimit.Exceeded', undefined, 42),
    );
  });

  it('ignores a Retry-After that is not whole seconds', async () => {
    const error = await failWith({ title: 'Too many requests' }, 429, {
      'Retry-After': 'Wed, 21 Oct 2026 07:28:00 GMT',
    });

    expect((error as ApiError).retryAfterSeconds).toBeUndefined();
  });

  it('maps an empty error body to a generic ApiError', async () => {
    const error = await failWith(null, 500);

    expect(error).toEqual(new ApiError(500, GENERIC_ERROR_TITLE));
  });
});

describe('errorMessage', () => {
  it('joins title and detail for a 400 with a detail', () => {
    const error = new ApiError(
      400,
      'Validation failed',
      'The cut-off date must not be later than today (UTC).',
    );

    expect(errorMessage(error)).toBe(
      'Validation failed: The cut-off date must not be later than today (UTC).',
    );
  });

  it('shows the title alone for a 400 without a detail', () => {
    expect(errorMessage(new ApiError(400, 'Validation failed'))).toBe('Validation failed');
  });

  it('shows title and detail for a 404', () => {
    const error = new ApiError(404, 'Resource not found', 'The submission does not exist.');

    expect(errorMessage(error)).toBe('Resource not found: The submission does not exist.');
  });

  it('explains a 429 with the retry delay', () => {
    const error = new ApiError(
      429,
      'Too many requests',
      'Server detail',
      'RateLimit.Exceeded',
      undefined,
      42,
    );

    expect(errorMessage(error)).toBe('Too many requests. Try again in 42 seconds.');
  });

  it('uses the singular for a one-second delay', () => {
    const error = new ApiError(429, 'Too many requests', undefined, undefined, undefined, 1);

    expect(errorMessage(error)).toBe('Too many requests. Try again in 1 second.');
  });

  it('explains a 429 without Retry-After', () => {
    expect(errorMessage(new ApiError(429, GENERIC_ERROR_TITLE))).toBe(
      'Too many requests. Try again in a moment.',
    );
  });

  it('never shows server text for a 500', () => {
    const error = new ApiError(500, 'An error occurred', 'Internal detail', 'X.Y');

    expect(errorMessage(error)).toBe(GENERIC_ERROR_TITLE);
  });

  it('shows the generic text for anything that is not an ApiError', () => {
    expect(errorMessage(new Error('socket hang up'))).toBe(GENERIC_ERROR_TITLE);
    expect(errorMessage('oops')).toBe(GENERIC_ERROR_TITLE);
  });
});
