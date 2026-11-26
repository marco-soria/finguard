import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { problemDetailsInterceptor } from '../../core/http/problem-details.interceptor';
import { NOT_FOUND_RETRY_DELAYS_MS } from '../../shared/retry-while-not-found';
import { AdvisoryPanel } from './advisory-panel';

describe('AdvisoryPanel', () => {
  let fixture: ComponentFixture<AdvisoryPanel>;
  let element: HTMLElement;
  let backend: HttpTestingController;

  const url = '/api/advisory/submissions/s-1/advisory';
  const advisory = {
    evaluationId: 'e-1',
    submissionId: 's-1',
    narrator: 'Template',
    generatedAt: '2026-11-19T10:00:02Z',
    paragraphs: [
      'On 2026-06-30, the portfolio breaches the limit for Shares under policy version 1.',
      'This text explains a deterministic calculation. It is not investment advice.',
    ],
  };

  const create = (hasProposal: boolean) => {
    fixture = TestBed.createComponent(AdvisoryPanel);
    fixture.componentRef.setInput('submissionId', 's-1');
    fixture.componentRef.setInput('hasProposal', hasProposal);
    element = fixture.nativeElement as HTMLElement;
    TestBed.tick();
  };

  /** A resource delivers its response in a microtask; then effects and change detection run. */
  const settle = async () => {
    await Promise.resolve();
    TestBed.tick();
  };

  const notWrittenYet = () =>
    backend.expectOne(url).flush(
      {
        title: 'Resource not found',
        status: 404,
        detail: 'The advisory does not exist.',
        code: 'Advisory.Advisory.NotFound',
      },
      { status: 404, statusText: 'Not Found' },
    );

  const state = () => element.querySelector('.advisory')?.getAttribute('data-advisory-state');
  const note = () => element.querySelector('.advisory [role="status"]')?.textContent?.trim();

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    backend.verify();
    vi.useRealTimers();
  });

  it('says there is no advisory and sends no request for an evaluation without a proposal', async () => {
    create(false);
    await settle();

    backend.expectNone(url);
    expect(state()).toBe('none');
    expect(note()).toBe('No advisory is available for this evaluation.');
  });

  it('shows the paragraphs and the source line when ready', async () => {
    create(true);
    backend.expectOne(url).flush(advisory);
    await fixture.whenStable();

    expect(state()).toBe('ready');
    expect(element.querySelector('#advisory-title')?.textContent?.trim()).toBe('Advisory');
    expect(
      Array.from(element.querySelectorAll('.advisory-text p')).map((p) => p.textContent?.trim()),
    ).toEqual(advisory.paragraphs);
    expect(element.querySelector('.advisory-source')?.textContent?.trim()).toBe(
      'Written from a template.',
    );
  });

  it('shows markup in a paragraph as text, never as HTML', async () => {
    create(true);
    backend.expectOne(url).flush({ ...advisory, paragraphs: ['<b>x</b>'] });
    await fixture.whenStable();

    const paragraph = element.querySelector('.advisory-text p');
    expect(paragraph?.textContent).toBe('<b>x</b>');
    expect(paragraph?.querySelector('b')).toBeNull();
  });

  it('waits while the advisory is not written yet and succeeds after a retry', async () => {
    vi.useFakeTimers();
    create(true);
    notWrittenYet();
    await settle();

    expect(state()).toBe('pending');
    expect(note()).toBe('Writing the advisory…');

    vi.advanceTimersByTime(NOT_FOUND_RETRY_DELAYS_MS[0]);
    await settle();
    backend.expectOne(url).flush(advisory);
    await settle();

    expect(state()).toBe('ready');
  });

  it('times out after the last retry and "Check again" starts over', async () => {
    vi.useFakeTimers();
    create(true);
    notWrittenYet();
    await settle();
    for (const delay of NOT_FOUND_RETRY_DELAYS_MS) {
      vi.advanceTimersByTime(delay);
      await settle();
      notWrittenYet();
      await settle();
    }

    expect(state()).toBe('timeout');
    expect(note()).toBe('The advisory is not ready yet.');
    (element.querySelector('.advisory button') as HTMLButtonElement).click();
    await settle();
    backend.expectOne(url).flush(advisory);
    await settle();

    expect(state()).toBe('ready');
  });

  it('shows the error message for any other error', async () => {
    create(true);
    backend
      .expectOne(url)
      .flush({ title: 'Server error', status: 500 }, { status: 500, statusText: 'Server Error' });
    await settle();

    expect(state()).toBe('error');
    expect(element.querySelector('.advisory [role="alert"]')?.textContent?.trim()).toBe(
      'Something went wrong. Please try again.',
    );
  });
});
