import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { ApiError } from './api-error';
import { GENERIC_ERROR_TITLE, problemDetailsInterceptor } from './problem-details.interceptor';

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

  async function failWith(body: object | string | null, status: number): Promise<unknown> {
    const call = firstValueFrom(http.get('/api/x'));
    backend.expectOne('/api/x').flush(body, { status, statusText: 'Error' });
    return call.then(
      () => null,
      (error: unknown) => error,
    );
  }

  it('maps ProblemDetails with code and traceId', async () => {
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
      new ApiError(403, 'Forbidden', 'Tenancy.User.NotProvisioned', '00-abc-def-01'),
    );
  });

  it('maps ProblemDetails without code or traceId', async () => {
    const error = await failWith({ title: 'Resource not found', status: 404 }, 404);

    expect(error).toEqual(new ApiError(404, 'Resource not found'));
  });

  it('maps a non-ProblemDetails error to a generic ApiError with the status', async () => {
    const error = await failWith('<html>Bad gateway: upstream secret detail</html>', 502);

    expect(error).toEqual(new ApiError(502, GENERIC_ERROR_TITLE));
  });

  it('maps an empty error body to a generic ApiError', async () => {
    const error = await failWith(null, 500);

    expect(error).toEqual(new ApiError(500, GENERIC_ERROR_TITLE));
  });
});
