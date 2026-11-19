import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SubmissionListItem } from '../../core/api/portfolio-api';
import { problemDetailsInterceptor } from '../../core/http/problem-details.interceptor';
import { SubmissionHistory } from './submission-history';

describe('SubmissionHistory', () => {
  let fixture: ComponentFixture<SubmissionHistory>;
  let element: HTMLElement;
  let backend: HttpTestingController;

  const item: SubmissionListItem = {
    submissionId: 's-1',
    cutOffDate: '2026-11-01',
    fileName: 'portfolio.csv',
    sizeBytes: 2048,
    status: 'Received',
    receivedAt: '2026-11-19T10:00:00Z',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SubmissionHistory],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SubmissionHistory);
    element = fixture.nativeElement as HTMLElement;
    // Not whenStable: the pending request keeps the app unstable until a test flushes it.
    TestBed.tick();
  });

  afterEach(() => backend.verify());

  async function respond(body: object, status = 200): Promise<void> {
    backend
      .expectOne('/api/portfolio/submissions')
      .flush(body, { status, statusText: status === 200 ? 'OK' : 'Error' });
    await fixture.whenStable();
  }

  const cells = () =>
    Array.from(element.querySelectorAll('tbody tr td, tr.mat-mdc-row td')).map((c) =>
      c.textContent?.trim(),
    );

  it('shows a loading indicator until the list arrives', async () => {
    expect(element.querySelector('mat-progress-bar')).not.toBeNull();

    await respond({ items: [] });

    expect(element.querySelector('mat-progress-bar')).toBeNull();
  });

  it('renders one row per submission with a link to its details', async () => {
    await respond({ items: [item] });

    expect(cells()).toEqual([
      '2026-11-01',
      'portfolio.csv',
      '2.0 KB',
      'Received',
      expect.any(String),
    ]);
    expect(element.querySelector('a')?.getAttribute('href')).toBe('/submissions/s-1');
  });

  it('renders the cut-off date exactly as the API returns it', async () => {
    // Late-in-day UTC dates shift one day when parsed as Date west of UTC (D-103); the text must stay as sent.
    await respond({ items: [{ ...item, cutOffDate: '2026-01-01' }] });

    expect(cells()[0]).toBe('2026-01-01');
  });

  it('shows the empty state', async () => {
    await respond({ items: [] });

    expect(element.textContent).toContain('No submissions yet.');
    expect(element.querySelector('table')).toBeNull();
  });

  it('shows the ApiError title on failure', async () => {
    await respond({ title: 'Forbidden', status: 403 }, 403);

    expect(element.querySelector('[role="alert"]')?.textContent?.trim()).toBe('Forbidden');
  });

  it('reloads on demand', async () => {
    await respond({ items: [] });

    fixture.componentInstance.reload();
    TestBed.tick();
    await respond({ items: [item] });

    expect(cells()[1]).toBe('portfolio.csv');
  });
});
