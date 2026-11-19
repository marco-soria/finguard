import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { problemDetailsInterceptor } from '../../core/http/problem-details.interceptor';
import { MAX_FILE_BYTES, UploadForm } from './upload-form';

describe('UploadForm', () => {
  let fixture: ComponentFixture<UploadForm>;
  let element: HTMLElement;
  let backend: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [UploadForm],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(UploadForm);
    element = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  });

  afterEach(() => backend.verify());

  const csv = (name = 'portfolio.csv', size = 10) =>
    new File([new Uint8Array(size)], name, { type: 'text/csv' });

  function setDate(value: string): void {
    const input = element.querySelector<HTMLInputElement>('input[type="date"]')!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function setFile(file: File): void {
    const input = element.querySelector<HTMLInputElement>('input[type="file"]')!;
    // jsdom has no DataTransfer, so a FileList-like object stands in for the user's choice.
    const files = { length: 1, 0: file, item: (i: number) => (i === 0 ? file : null) };
    Object.defineProperty(input, 'files', { configurable: true, value: files });
    input.dispatchEvent(new Event('change'));
  }

  // Not whenStable: an open upload request keeps the app unstable until a test flushes it.
  function submit(): void {
    element.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    TestBed.tick();
  }

  const submitButton = () => element.querySelector<HTMLButtonElement>('button[type="submit"]')!;
  const text = (selector: string) => element.querySelector(selector)?.textContent?.trim();

  it('sends nothing without a file or a date', async () => {
    submit();

    backend.expectNone('/api/portfolio/submissions');
    expect(text('#portfolio-file-error')).toBe('Choose a CSV file.');
    expect(element.textContent).toContain('Enter the cut-off date.');
  });

  it.each([
    ['portfolio.txt', 10, 'The file must have the .csv extension.'],
    ['portfolio.csv', 0, 'The file is empty.'],
    ['portfolio.csv', MAX_FILE_BYTES + 1, 'The file is larger than 5 MB.'],
  ])('rejects %s of %d bytes before any request', async (name, size, message) => {
    setDate('2026-11-19');
    setFile(csv(name, size));

    submit();

    backend.expectNone('/api/portfolio/submissions');
    expect(text('#portfolio-file-error')).toBe(message);
  });

  it('disables submit while sending, then emits uploaded and resets', async () => {
    const uploaded = vi.fn();
    fixture.componentInstance.uploaded.subscribe(uploaded);
    setDate('2026-11-19');
    setFile(csv());

    submit();

    expect(submitButton().disabled).toBe(true);
    const response = {
      submissionId: 's-1',
      status: 'Accepted',
      receivedAt: '2026-11-19T10:00:00Z',
      rowCount: 12,
      errorCount: 0,
      errors: [],
    };
    backend.expectOne('/api/portfolio/submissions').flush(response);
    await fixture.whenStable();

    expect(submitButton().disabled).toBe(false);
    expect(uploaded).toHaveBeenCalledWith(response);
    expect(text('.upload-outcome')).toBe('Accepted portfolio.csv: 12 positions.');
    const link = element.querySelector<HTMLAnchorElement>('[role="status"] a')!;
    expect(link.textContent?.trim()).toBe('View details');
    expect(link.getAttribute('href')).toBe('/submissions/s-1');
    expect(element.querySelector<HTMLInputElement>('input[type="date"]')!.value).toBe('');
    expect(element.querySelector<HTMLInputElement>('input[type="file"]')!.value).toBe('');
    // The reset form is a fresh form: no field errors until the next submit.
    expect(element.textContent).not.toContain('Enter the cut-off date.');
    expect(element.querySelector('#portfolio-file-error')).toBeNull();
  });

  it('reports a rejected upload with its error count and the details link', async () => {
    setDate('2026-11-19');
    setFile(csv('bad.csv'));

    submit();
    backend.expectOne('/api/portfolio/submissions').flush({
      submissionId: 's-2',
      status: 'Rejected',
      receivedAt: '2026-11-19T10:00:00Z',
      rowCount: 3,
      errorCount: 4,
      errors: [],
    });
    await fixture.whenStable();

    expect(text('.upload-outcome')).toBe('Rejected bad.csv: 4 errors.');
    expect(element.querySelector('[role="status"] a')?.getAttribute('href')).toBe(
      '/submissions/s-2',
    );
    // A rejected file is a created submission, not a failed request.
    expect(element.querySelector('[role="alert"]')).toBeNull();
  });

  it('ignores a second submit while sending', async () => {
    setDate('2026-11-19');
    setFile(csv());

    submit();
    submit();

    backend.expectOne('/api/portfolio/submissions').flush({});
  });

  it('shows the ApiError title on failure', async () => {
    setDate('2026-11-19');
    setFile(csv());

    submit();
    backend
      .expectOne('/api/portfolio/submissions')
      .flush(
        { title: 'Validation failed', status: 400 },
        { status: 400, statusText: 'Bad Request' },
      );
    await fixture.whenStable();

    expect(text('[role="alert"]')).toBe('Validation failed');
    expect(submitButton().disabled).toBe(false);
  });
});
