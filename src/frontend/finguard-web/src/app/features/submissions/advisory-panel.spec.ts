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
    model: null,
    refinement: 'NotRequested',
    aiContext: null,
  };
  const pending = { ...advisory, refinement: 'Pending' };
  const contextText =
    'Holding too much of the portfolio in Shares concentrates its risk in one kind of asset.';
  const withContext = {
    ...advisory,
    model: 'llama3.2:3b',
    refinement: 'Accepted',
    aiContext: contextText,
  };
  const contextLabel =
    'Written by an AI model (llama3.2:3b). It contains no figures or statuses; all figures above come from the evaluation.';

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
  const source = () => element.querySelector('.advisory-source')?.textContent?.trim();
  const refiningNote = () => element.querySelector('.advisory-refining')?.textContent?.trim();

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

  const contextSection = () => element.querySelector('.advisory-context');
  const shownContext = () => ({
    heading: contextSection()?.querySelector('#advisory-context-title')?.textContent?.trim(),
    text: contextSection()?.querySelector('.advisory-context-text')?.textContent?.trim(),
    label: contextSection()?.querySelector('.advisory-context-label')?.textContent?.trim(),
  });

  it('shows the template and then a Context subsection with the model label when accepted', async () => {
    create(true);
    backend.expectOne(url).flush(withContext);
    await fixture.whenStable();

    expect(
      Array.from(element.querySelectorAll('.advisory-text p')).map((p) => p.textContent?.trim()),
    ).toEqual(advisory.paragraphs);
    expect(source()).toBe('Written from a template.');
    expect(shownContext()).toEqual({ heading: 'Context', text: contextText, label: contextLabel });
    expect(refiningNote()).toBeUndefined();
  });

  it('shows markup in the context as text, never as HTML', async () => {
    create(true);
    backend.expectOne(url).flush({ ...withContext, aiContext: '<b>Shares</b>' });
    await fixture.whenStable();

    expect(contextSection()?.querySelector('b')).toBeNull();
    expect(shownContext().text).toBe('<b>Shares</b>');
  });

  it('fails closed for an unknown narrator: template label and no Context subsection', async () => {
    create(true);
    backend.expectOne(url).flush({ ...withContext, narrator: 'Model' });
    await fixture.whenStable();

    expect(source()).toBe('Written from a template.');
    expect(contextSection()).toBeNull();
  });

  it('shows the template and the pending note while the AI context is added', async () => {
    vi.useFakeTimers();
    create(true);
    backend.expectOne(url).flush(pending);
    await settle();

    expect(state()).toBe('ready');
    expect(source()).toBe('Written from a template.');
    expect(refiningNote()).toBe('Adding AI context…');
    expect(contextSection()).toBeNull();
  });

  it('adds the Context subsection when a reload finds the accepted context', async () => {
    vi.useFakeTimers();
    create(true);
    backend.expectOne(url).flush(pending);
    await settle();

    vi.advanceTimersByTime(NOT_FOUND_RETRY_DELAYS_MS[0]);
    await settle();
    backend.expectOne(url).flush(withContext);
    await settle();

    expect(shownContext().label).toBe(contextLabel);
    expect(refiningNote()).toBeUndefined();
  });

  it('stops polling silently after the last delay and keeps the template', async () => {
    vi.useFakeTimers();
    create(true);
    backend.expectOne(url).flush(pending);
    await settle();
    for (const delay of NOT_FOUND_RETRY_DELAYS_MS) {
      vi.advanceTimersByTime(delay);
      await settle();
      backend.expectOne(url).flush(pending);
      await settle();
    }

    vi.advanceTimersByTime(60_000);
    await settle();

    backend.expectNone(url);
    expect(refiningNote()).toBeUndefined();
    expect(source()).toBe('Written from a template.');
    expect(contextSection()).toBeNull();
  });

  it('shows no Context subsection when the context was rejected or not requested', async () => {
    create(true);
    backend.expectOne(url).flush({ ...advisory, refinement: 'Rejected' });
    await fixture.whenStable();

    expect(source()).toBe('Written from a template.');
    expect(contextSection()).toBeNull();
    expect(refiningNote()).toBeUndefined();
  });
});
