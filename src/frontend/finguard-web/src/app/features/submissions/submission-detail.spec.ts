import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { problemDetailsInterceptor } from '../../core/http/problem-details.interceptor';
import { NOT_FOUND_RETRY_DELAYS_MS as EVALUATION_RETRY_DELAYS_MS } from '../../shared/retry-while-not-found';
import SubmissionDetail, { changeText } from './submission-detail';

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

  afterEach(() => {
    // The advisory panel has its own spec; here its request only follows a shown proposal.
    backend.match(advisoryUrl);
    backend.verify();
  });

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

  const detailUrl = '/api/v1/portfolio/submissions/s-1';
  const evaluationUrl = '/api/v1/compliance/submissions/s-1/evaluation';
  const advisoryUrl = '/api/v1/advisory/submissions/s-1/advisory';

  const evaluation = {
    evaluationId: 'e-1',
    submissionId: 's-1',
    cutOffDate: '2026-11-01',
    overallStatus: 'Breached',
    policyVersion: 1,
    evaluatedAt: '2026-11-19T10:00:01Z',
    totalBookValue: 3000000,
    alertThreshold: 0.9,
    classes: [
      {
        assetClass: 'Cash',
        bookValue: 300000,
        share: 0.1,
        limit: 0.4,
        utilization: 0.25,
        status: 'Compliant',
      },
      {
        assetClass: 'FixedIncome',
        bookValue: 800000,
        share: 0.2666666667,
        limit: 0.3,
        utilization: 0.89999996,
        status: 'Compliant',
      },
      {
        assetClass: 'Shares',
        bookValue: 1800000,
        share: 0.6,
        limit: 0.25,
        utilization: 2.4,
        status: 'Breached',
      },
      {
        assetClass: 'RealEstate',
        bookValue: 100000,
        share: 0.0333333333,
        limit: 0.25,
        utilization: 1,
        status: 'Warning',
      },
    ],
    alerts: [
      { assetClass: 'Shares', status: 'Breached', utilization: 2.4 },
      { assetClass: 'RealEstate', status: 'Warning', utilization: 1 },
    ],
  };

  const notEvaluatedYet = () =>
    backend.expectOne(evaluationUrl).flush(
      {
        title: 'Resource not found',
        status: 404,
        detail: 'The evaluation does not exist.',
        code: 'Compliance.Evaluation.NotFound',
      },
      { status: 404, statusText: 'Not Found' },
    );

  /** A resource delivers its response in a microtask; then effects and change detection run. */
  const settle = async () => {
    await Promise.resolve();
    TestBed.tick();
  };

  /** Flushes the accepted submission and lets the component send the evaluation request. */
  const acceptSubmission = async () => {
    backend.expectOne(detailUrl).flush(details);
    await settle();
  };

  const state = () => element.querySelector('.compliance')?.getAttribute('data-state');

  const classRows = () =>
    Array.from(element.querySelectorAll('.compliance-classes tr.mat-mdc-row')).map((tr) => ({
      assetClass: tr.getAttribute('data-class'),
      cells: Array.from(tr.querySelectorAll('td')).map((td) => td.textContent?.trim()),
    }));

  const errorRows = () =>
    Array.from(element.querySelectorAll('.detail-errors tr.mat-mdc-row')).map((tr) => ({
      code: tr.getAttribute('data-code'),
      cells: Array.from(tr.querySelectorAll('td')).map((td) => td.textContent?.trim()),
    }));

  it('renders the fields of an accepted submission without an error list', async () => {
    await acceptSubmission();
    backend.expectOne(evaluationUrl).flush(evaluation);
    await fixture.whenStable();

    const values = Array.from(element.querySelector('dl')!.querySelectorAll('dd')).map((d) =>
      d.textContent?.trim(),
    );
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
    backend.expectOne(detailUrl).flush({ ...details, isCurrent: false });
    await settle();
    backend.expectOne(evaluationUrl).flush(evaluation);
    await fixture.whenStable();

    expect(element.querySelector('.detail-version')?.textContent?.trim()).toBe(
      'Not the current version',
    );
  });

  it('lists the errors of a rejected submission in a table with codes as data only', async () => {
    backend.expectOne('/api/v1/portfolio/submissions/s-1').flush({
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
    backend.expectOne('/api/v1/portfolio/submissions/s-1').flush({
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
    backend.expectOne('/api/v1/portfolio/submissions/s-1').flush(
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
  describe('compliance section', () => {
    afterEach(() => vi.useRealTimers());

    it('requests the evaluation of an accepted submission and shows the result', async () => {
      await acceptSubmission();
      backend.expectOne(evaluationUrl).flush(evaluation);
      await fixture.whenStable();

      expect(state()).toBe('ready');
      expect(element.querySelector('.compliance-overall')?.textContent?.trim()).toBe('Breached');
      expect(
        element.querySelector('.compliance-overall .status-pill')?.getAttribute('data-status'),
      ).toBe('Breached');
      expect(element.querySelector('.compliance-policy')?.textContent?.trim()).toBe(
        'Policy version 1',
      );
      expect(element.querySelector('.compliance')?.textContent).toContain('$3,000,000.00');
      expect(element.querySelector('.compliance-classes caption')).not.toBeNull();
    });

    it('lists one row per class in API order with formatted values', async () => {
      await acceptSubmission();
      backend.expectOne(evaluationUrl).flush(evaluation);
      await fixture.whenStable();

      expect(classRows()).toEqual([
        {
          assetClass: 'Cash',
          cells: ['Cash', '$300,000.00', '10.00 %', '40.00 %', '25.00 %', 'Compliant'],
        },
        {
          assetClass: 'FixedIncome',
          cells: ['Fixed income', '$800,000.00', '26.67 %', '30.00 %', '89.999996 %', 'Compliant'],
        },
        {
          assetClass: 'Shares',
          cells: ['Shares', '$1,800,000.00', '60.00 %', '25.00 %', '240.00 %', 'Breached'],
        },
        {
          assetClass: 'RealEstate',
          cells: ['Real estate', '$100,000.00', '3.33 %', '25.00 %', '100.00 %', 'Warning'],
        },
      ]);
    });

    it('describes each alert in words', async () => {
      await acceptSubmission();
      backend.expectOne(evaluationUrl).flush(evaluation);
      await fixture.whenStable();

      const alerts = Array.from(element.querySelectorAll('.compliance-alerts li')).map((li) =>
        li.textContent?.trim(),
      );
      expect(alerts).toEqual([
        'Shares is over its limit (240.00 % of the limit used).',
        'Real estate is near its limit (100.00 % of the limit used).',
      ]);
    });

    it('says when there are no alerts', async () => {
      await acceptSubmission();
      backend
        .expectOne(evaluationUrl)
        .flush({ ...evaluation, overallStatus: 'Compliant', alerts: [] });
      await fixture.whenStable();

      expect(element.querySelector('.compliance-no-alerts')?.textContent?.trim()).toBe(
        'No alerts.',
      );
      expect(element.querySelector('.compliance-alerts')).toBeNull();
    });

    it('never requests the evaluation of a rejected submission', async () => {
      backend.expectOne(detailUrl).flush({ ...details, status: 'Rejected' });
      await fixture.whenStable();

      backend.expectNone(evaluationUrl);
      expect(element.querySelector('.compliance')).toBeNull();
    });

    it('shows "Evaluating…" in a live region and succeeds after a retry', async () => {
      vi.useFakeTimers();
      await acceptSubmission();
      notEvaluatedYet();
      await settle();

      expect(state()).toBe('pending');
      const live = element.querySelector('.compliance [role="status"]');
      expect(live?.getAttribute('aria-live')).toBe('polite');
      expect(live?.textContent?.trim()).toBe('Evaluating…');

      vi.advanceTimersByTime(EVALUATION_RETRY_DELAYS_MS[0]);
      await settle();
      backend.expectOne(evaluationUrl).flush(evaluation);
      await settle();

      expect(state()).toBe('ready');
      expect(vi.getTimerCount()).toBe(0);
    });

    it('gives up after the last retry and "Check again" starts over', async () => {
      vi.useFakeTimers();
      await acceptSubmission();
      notEvaluatedYet();
      await settle();
      for (const delay of EVALUATION_RETRY_DELAYS_MS) {
        expect(state()).toBe('pending');
        vi.advanceTimersByTime(delay);
        await settle();
        notEvaluatedYet();
        await settle();
      }

      expect(state()).toBe('timeout');
      expect(element.querySelector('.compliance [role="status"]')?.textContent?.trim()).toBe(
        'The evaluation is not available yet.',
      );
      expect(vi.getTimerCount()).toBe(0);

      const button = Array.from(element.querySelectorAll('.compliance button')).find(
        (b) => b.textContent?.trim() === 'Check again',
      ) as HTMLButtonElement;
      button.click();
      await settle();
      backend.expectOne(evaluationUrl).flush(evaluation);
      await settle();

      expect(state()).toBe('ready');
    });

    it('shows the error message for any other error and stops polling', async () => {
      vi.useFakeTimers();
      await acceptSubmission();
      backend
        .expectOne(evaluationUrl)
        .flush({ title: 'Server error', status: 500 }, { status: 500, statusText: 'Server Error' });
      await settle();

      expect(state()).toBe('error');
      expect(element.querySelector('.compliance [role="alert"]')?.textContent?.trim()).toBe(
        'Something went wrong. Please try again.',
      );
      expect(vi.getTimerCount()).toBe(0);
    });

    it('stops polling when the page is destroyed', async () => {
      vi.useFakeTimers();
      await acceptSubmission();
      notEvaluatedYet();
      await settle();
      expect(vi.getTimerCount()).toBeGreaterThan(0);

      fixture.destroy();
      // Angular may leave its own timers; what matters is that no retry request follows.
      vi.advanceTimersByTime(60_000);
      backend.expectNone(evaluationUrl);
    });
  });

  describe('rebalancing proposal', () => {
    // Shares-heavy sample as the API returns it: the reducer first, then receivers by amount.
    const fullProposal = {
      status: 'Full',
      moves: [
        {
          assetClass: 'Shares',
          currentBookValue: 2200000,
          proposedBookValue: 637500,
          change: -1562500,
        },
        {
          assetClass: 'Cash',
          currentBookValue: 300000,
          proposedBookValue: 993374.43,
          change: 693374.43,
        },
        {
          assetClass: 'RealEstate',
          currentBookValue: 100000,
          proposedBookValue: 617623.26,
          change: 517623.26,
        },
        {
          assetClass: 'FixedIncome',
          currentBookValue: 400000,
          proposedBookValue: 751502.31,
          change: 351502.31,
        },
      ],
    };

    const showProposal = async (proposal: unknown) => {
      await acceptSubmission();
      backend.expectOne(evaluationUrl).flush({ ...evaluation, proposal });
      // Not whenStable: the advisory request stays open unless a test flushes it.
      await settle();
    };

    const section = () => element.querySelector('.proposal');
    const summary = () => element.querySelector('.proposal-summary')?.textContent?.trim();
    const rows = (selector: string) =>
      Array.from(element.querySelectorAll(`.proposal-moves ${selector}`)).map((tr) =>
        Array.from(tr.querySelectorAll('th, td')).map((cell) => cell.textContent?.trim()),
      );

    it('says no rebalancing is needed and shows no table', async () => {
      await showProposal({ status: 'NotNeeded', moves: [] });

      expect(section()?.getAttribute('data-proposal-status')).toBe('NotNeeded');
      expect(summary()).toBe('No rebalancing needed.');
      expect(element.querySelector('.proposal-moves')).toBeNull();
    });

    it('lists the moves in API order with signed changes and a totals row', async () => {
      await showProposal(fullProposal);

      expect(section()?.getAttribute('data-proposal-status')).toBe('Full');
      expect(summary()).toBe('Moving the amounts below brings every class within its limit.');
      expect(element.querySelector('.proposal-moves caption')).not.toBeNull();
      expect(rows('tr.mat-mdc-row')).toEqual([
        ['Shares', '$2,200,000.00', '$637,500.00', '−$1,562,500.00 (decrease)'],
        ['Cash', '$300,000.00', '$993,374.43', '+$693,374.43 (increase)'],
        ['Real estate', '$100,000.00', '$617,623.26', '+$517,623.26 (increase)'],
        ['Fixed income', '$400,000.00', '$751,502.31', '+$351,502.31 (increase)'],
      ]);
      expect(rows('tr.proposal-total')).toEqual([
        ['Total', '$3,000,000.00', '$3,000,000.00', '$0.00 (no change)'],
      ]);
    });

    it('explains a partial proposal', async () => {
      await showProposal({ ...fullProposal, status: 'Partial' });

      expect(section()?.getAttribute('data-proposal-status')).toBe('Partial');
      expect(summary()).toBe(
        'Partial: rounding leaves a class outside its target; review the amounts.',
      );
    });

    it('says when an older evaluation has no proposal', async () => {
      await showProposal(null);

      expect(section()?.getAttribute('data-proposal-status')).toBe('None');
      expect(summary()).toBe('No proposal is available for this evaluation.');
      expect(element.querySelector('.proposal-moves')).toBeNull();
    });

    it('shows the advisory under the proposal', async () => {
      await showProposal(fullProposal);
      backend.expectOne(advisoryUrl).flush({
        evaluationId: 'e-1',
        submissionId: 's-1',
        narrator: 'Template',
        generatedAt: '2026-11-19T10:00:02Z',
        paragraphs: ['First paragraph.', 'Second paragraph.'],
      });
      await settle();

      const advisory = element.querySelector('.proposal + app-advisory-panel .advisory');
      expect(advisory?.getAttribute('data-advisory-state')).toBe('ready');
      expect(advisory?.querySelectorAll('.advisory-text p').length).toBe(2);
    });

    it('requests no advisory for an older evaluation without a proposal', async () => {
      await showProposal(null);

      backend.expectNone(advisoryUrl);
      expect(element.querySelector('.advisory')?.getAttribute('data-advisory-state')).toBe('none');
    });
  });
});

describe('changeText', () => {
  it.each([
    [693374.43, '+$693,374.43 (increase)'],
    [-1562500, '−$1,562,500.00 (decrease)'],
    [0, '$0.00 (no change)'],
    // A floating-point sum of exact cents never shows as "-$0.00".
    [0.1 + 0.2 - 0.3, '$0.00 (no change)'],
  ])('formats %d as %s', (change, expected) => {
    expect(changeText(change)).toBe(expected);
  });
});
