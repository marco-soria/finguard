import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { problemDetailsInterceptor } from '../../core/http/problem-details.interceptor';
import SubmissionDetail from './submission-detail';

describe('SubmissionDetail', () => {
  let fixture: ComponentFixture<SubmissionDetail>;
  let element: HTMLElement;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SubmissionDetail],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SubmissionDetail);
    fixture.componentRef.setInput('id', 's-1');
    element = fixture.nativeElement as HTMLElement;
    // Not whenStable: the pending request keeps the app unstable until a test flushes it.
    TestBed.tick();
  });

  afterEach(() => backend.verify());

  const details = {
    submissionId: 's-1',
    cutOffDate: '2026-11-01',
    fileName: 'portfolio.csv',
    sizeBytes: 512,
    status: 'Accepted',
    receivedAt: '2026-11-19T10:00:00Z',
    rowCount: 4,
    errorCount: 0,
    isCurrent: true,
    errors: [],
  };

  const errorRows = () =>
    Array.from(element.querySelectorAll('.detail-errors tr.mat-mdc-row')).map((tr) => ({
      code: tr.getAttribute('data-code'),
      cells: Array.from(tr.querySelectorAll('td')).map((td) => td.textContent?.trim()),
    }));

  it('renders the fields of an accepted submission without an error list', async () => {
    backend.expectOne('/api/portfolio/submissions/s-1').flush(details);
    await fixture.whenStable();

    const values = Array.from(element.querySelectorAll('dd')).map((d) => d.textContent?.trim());
    expect(values).toEqual([
      'portfolio.csv',
      '2026-11-01',
      '512 B',
      'Accepted',
      'Current version for 2026-11-01',
      '4',
      '0',
      expect.any(String),
      's-1',
    ]);
    expect(element.querySelector('a')?.textContent?.trim()).toBe('Back to submissions');
    expect(element.querySelector('.detail-errors')).toBeNull();
  });

  it('says when an accepted submission is not the current version', async () => {
    backend.expectOne('/api/portfolio/submissions/s-1').flush({ ...details, isCurrent: false });
    await fixture.whenStable();

    expect(element.querySelector('.detail-version')?.textContent?.trim()).toBe(
      'Not the current version',
    );
  });

  it('lists the errors of a rejected submission in a table with codes as data only', async () => {
    backend.expectOne('/api/portfolio/submissions/s-1').flush({
      ...details,
      status: 'Rejected',
      rowCount: 2,
      errorCount: 3,
      errors: [
        { row: null, column: null, code: 'Portfolio.Csv.NoRows', message: 'The file has no rows.' },
        { row: null, column: 'currency', code: 'Portfolio.Csv.ColumnMissing', message: 'Missing.' },
        {
          row: 3,
          column: 'book_value',
          code: 'Portfolio.Csv.BookValueNegative',
          message: 'The book value must not be negative.',
        },
      ],
    });
    await fixture.whenStable();

    expect(errorRows()).toEqual([
      { code: 'Portfolio.Csv.NoRows', cells: ['File', '', 'The file has no rows.'] },
      { code: 'Portfolio.Csv.ColumnMissing', cells: ['File', 'currency', 'Missing.'] },
      {
        code: 'Portfolio.Csv.BookValueNegative',
        cells: ['3', 'book_value', 'The book value must not be negative.'],
      },
    ]);
    expect(element.querySelector('.detail-errors')?.getAttribute('aria-labelledby')).toBe(
      'errors-title',
    );
    expect(element.textContent).not.toContain('Portfolio.Csv.');
    expect(element.querySelector('.detail-version')).toBeNull();
  });

  it('says when only the first 100 errors are shown', async () => {
    backend.expectOne('/api/portfolio/submissions/s-1').flush({
      ...details,
      status: 'Rejected',
      errorCount: 150,
      errors: Array.from({ length: 100 }, (_, i) => ({
        row: i + 2,
        column: 'currency',
        code: 'X',
        message: 'Bad.',
      })),
    });
    await fixture.whenStable();

    expect(element.textContent).toContain('Showing the first 100 of 150 errors.');
    expect(errorRows().length).toBe(100);
  });

  it('shows the error title and detail on 404', async () => {
    backend.expectOne('/api/portfolio/submissions/s-1').flush(
      {
        title: 'Resource not found',
        status: 404,
        detail: 'The submission does not exist.',
        code: 'Portfolio.Submission.NotFound',
      },
      { status: 404, statusText: 'Not Found' },
    );
    await fixture.whenStable();

    expect(element.querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      'Resource not found: The submission does not exist.',
    );
    expect(element.querySelector('dl')).toBeNull();
  });
});
