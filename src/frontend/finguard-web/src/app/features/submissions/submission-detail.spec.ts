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

  it('renders the fields of the submission', async () => {
    backend.expectOne('/api/portfolio/submissions/s-1').flush({
      submissionId: 's-1',
      cutOffDate: '2026-11-01',
      fileName: 'portfolio.csv',
      sizeBytes: 512,
      status: 'Received',
      receivedAt: '2026-11-19T10:00:00Z',
    });
    await fixture.whenStable();

    const values = Array.from(element.querySelectorAll('dd')).map((d) => d.textContent?.trim());
    expect(values).toEqual([
      'portfolio.csv',
      '2026-11-01',
      '512 B',
      'Received',
      expect.any(String),
      's-1',
    ]);
    expect(element.querySelector('a')?.textContent?.trim()).toBe('Back to submissions');
  });

  it('shows the error title on 404', async () => {
    backend
      .expectOne('/api/portfolio/submissions/s-1')
      .flush(
        { title: 'Resource not found', status: 404, code: 'Portfolio.Submission.NotFound' },
        { status: 404, statusText: 'Not Found' },
      );
    await fixture.whenStable();

    expect(element.querySelector('[role="alert"]')?.textContent?.trim()).toBe('Resource not found');
    expect(element.querySelector('dl')).toBeNull();
  });
});
