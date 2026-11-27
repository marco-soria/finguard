import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { SubmissionListItem } from '../../core/api/portfolio-api';
import { problemDetailsInterceptor } from '../../core/http/problem-details.interceptor';
import { SubmissionHistory } from './submission-history';

describe('SubmissionHistory', () => {
  let fixture: ComponentFixture<SubmissionHistory>;
  let element: HTMLElement;
  let backend: HttpTestingController;
  let router: Router;

  const listUrl = '/api/v1/portfolio/submissions';

  const item: SubmissionListItem = {
    submissionId: 's-1',
    cutOffDate: '2026-11-01',
    fileName: 'portfolio.csv',
    sizeBytes: 2048,
    status: 'Accepted',
    receivedAt: '2026-11-19T10:00:00Z',
    rowCount: 3,
    errorCount: 0,
    isCurrent: false,
  };

  const named = (submissionId: string, fileName = `${submissionId}.csv`) => ({
    ...item,
    submissionId,
    fileName,
  });

  async function create(url = '/'): Promise<void> {
    TestBed.configureTestingModule({
      imports: [SubmissionHistory],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    await router.navigateByUrl(url);
    fixture = TestBed.createComponent(SubmissionHistory);
    element = fixture.nativeElement as HTMLElement;
    // Not whenStable: the pending request keeps the app unstable until a test flushes it.
    TestBed.tick();
  }

  afterEach(() => backend.verify());

  const nextRequest = (): TestRequest => backend.expectOne((r) => r.url === listUrl);

  async function respond(body: object, status = 200): Promise<TestRequest> {
    const request = nextRequest();
    request.flush(body, { status, statusText: status === 200 ? 'OK' : 'Error' });
    await fixture.whenStable();
    return request;
  }

  const page = (items: SubmissionListItem[], nextCursor: string | null = null) => ({
    items,
    nextCursor,
  });

  const cells = () =>
    Array.from(element.querySelectorAll('tbody tr td, tr.mat-mdc-row td')).map((c) =>
      c.textContent?.trim(),
    );

  const fileNames = () =>
    Array.from(element.querySelectorAll('tbody a')).map((a) => a.textContent?.trim());

  const loadMoreButton = () =>
    Array.from(element.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === 'Load more',
    );

  const component = () => fixture.componentInstance;

  describe('list', () => {
    beforeEach(() => create());

    it('shows a loading indicator until the list arrives', async () => {
      expect(element.querySelector('mat-progress-bar')).not.toBeNull();

      await respond(page([]));

      expect(element.querySelector('mat-progress-bar')).toBeNull();
    });

    it('asks for the first page without filters or cursor', async () => {
      const request = await respond(page([]));

      expect(request.request.params.keys()).toEqual([]);
    });

    it('renders one row per submission with a link to its details', async () => {
      await respond(page([item]));

      expect(cells()).toEqual([
        '2026-11-01',
        'portfolio.csv',
        '2.0 KB',
        'Accepted',
        '',
        expect.any(String),
      ]);
      expect(element.querySelector('tbody a')?.getAttribute('href')).toBe('/submissions/s-1');
    });

    it('marks the current submission with a text badge', async () => {
      await respond(
        page([
          { ...item, submissionId: 's-2', isCurrent: true },
          { ...item, submissionId: 's-1', isCurrent: false },
        ]),
      );

      const badges = element.querySelectorAll('.current-badge');
      expect(badges.length).toBe(1);
      expect(badges[0].textContent?.trim()).toBe('Current');
      expect(cells()[3]?.replace(/\s+/g, ' ')).toBe('Accepted Current');
      expect(cells()[9]).toBe('Accepted');
    });

    it('shows the error count for a rejected submission only', async () => {
      await respond(
        page([
          { ...item, submissionId: 's-2', status: 'Rejected', errorCount: 7 },
          { ...item, errorCount: 0 },
        ]),
      );

      const headers = Array.from(element.querySelectorAll('th')).map((h) => h.textContent?.trim());
      expect(headers).toContain('Errors');
      expect([cells()[4], cells()[10]]).toEqual(['7', '']);
    });

    it('renders the cut-off date exactly as the API returns it', async () => {
      // Late-in-day UTC dates shift one day when parsed as Date west of UTC (D-103); the text must stay as sent.
      await respond(page([{ ...item, cutOffDate: '2026-01-01' }]));

      expect(cells()[0]).toBe('2026-01-01');
    });

    it('shows the empty state without filters', async () => {
      await respond(page([]));

      expect(element.textContent).toContain('No submissions yet.');
      expect(element.querySelector('table')).toBeNull();
    });

    it('shows the ApiError title on failure', async () => {
      await respond({ title: 'Forbidden', status: 403 }, 403);

      expect(element.querySelector('[role="alert"]')?.textContent?.trim()).toBe('Forbidden');
    });

    it('keeps working after a failed page', async () => {
      await respond({ title: 'Validation failed', status: 400 }, 400);

      component().reload();
      TestBed.tick();
      await respond(page([item]));

      expect(element.querySelector('[role="alert"]')).toBeNull();
      expect(fileNames()).toEqual(['portfolio.csv']);
    });

    it('reloads the first page on demand', async () => {
      await respond(page([]));

      component().reload();
      TestBed.tick();
      await respond(page([item]));

      expect(cells()[1]).toBe('portfolio.csv');
    });
  });

  describe('load more', () => {
    beforeEach(() => create());

    it('appends the next page and hides the button at the end', async () => {
      await respond(page([named('s-1'), named('s-2')], 'cursor-1'));
      expect(loadMoreButton()).toBeDefined();

      loadMoreButton()!.click();
      TestBed.tick();
      const second = await respond(page([named('s-3')], null));

      expect(second.request.params.get('cursor')).toBe('cursor-1');
      expect(fileNames()).toEqual(['s-1.csv', 's-2.csv', 's-3.csv']);
      expect(loadMoreButton()).toBeUndefined();
    });

    it('is not shown when the first page is the last', async () => {
      await respond(page([item], null));

      expect(loadMoreButton()).toBeUndefined();
    });

    it('starts again from the first page after a reload', async () => {
      await respond(page([named('s-1')], 'cursor-1'));
      loadMoreButton()!.click();
      TestBed.tick();
      await respond(page([named('s-2')], null));

      component().reload();
      TestBed.tick();
      const request = await respond(page([named('s-9')], null));

      expect(request.request.params.has('cursor')).toBe(false);
      expect(fileNames()).toEqual(['s-9.csv']);
    });
  });

  describe('filters', () => {
    it('sends the filters from the URL to the API', async () => {
      await create('/?status=Rejected&cutOffFrom=2026-01-01&cutOffTo=2026-01-31&currentOnly=true');

      const request = await respond(page([]));

      expect(request.request.params.toString()).toBe(
        'status=Rejected&cutOffFrom=2026-01-01&cutOffTo=2026-01-31&currentOnly=true',
      );
    });

    it('shows the filters from the URL in the controls', async () => {
      await create('/?cutOffFrom=2026-01-01&currentOnly=true');
      await respond(page([]));

      const dates = element.querySelectorAll<HTMLInputElement>('input[type="date"]');
      expect(dates[0].value).toBe('2026-01-01');
      expect(dates[1].value).toBe('');
      expect(element.querySelector('mat-slide-toggle button')?.getAttribute('aria-checked')).toBe(
        'true',
      );
    });

    it('writes a changed filter to the URL and loads the first page again', async () => {
      await create();
      await respond(page([named('s-1')], 'cursor-1'));

      component()['setFilter']('status', 'Accepted');
      await fixture.whenStable();
      TestBed.tick();
      const request = await respond(page([named('s-2')]));

      expect(router.url).toBe('/?status=Accepted');
      expect(request.request.params.toString()).toBe('status=Accepted');
      expect(request.request.params.has('cursor')).toBe(false);
      expect(fileNames()).toEqual(['s-2.csv']);
    });

    it('removes a cleared filter from the URL', async () => {
      await create('/?status=Rejected&cutOffTo=2026-01-31');
      await respond(page([]));

      component()['setFilter']('status', '');
      await fixture.whenStable();
      TestBed.tick();
      await respond(page([]));

      expect(router.url).toBe('/?cutOffTo=2026-01-31');
    });

    it('writes currentOnly only when it is on', async () => {
      await create('/?currentOnly=true');
      await respond(page([]));

      component()['setFilter']('currentOnly', false);
      await fixture.whenStable();
      TestBed.tick();
      await respond(page([]));

      expect(router.url).toBe('/');
    });

    it('shows a filtered empty state and clears every filter', async () => {
      await create('/?status=Rejected&currentOnly=true');
      await respond(page([]));

      expect(element.textContent).toContain('No submissions match these filters.');
      expect(element.textContent).not.toContain('No submissions yet.');

      Array.from(element.querySelectorAll('button'))
        .find((b) => b.textContent?.trim() === 'Clear filters')!
        .click();
      await fixture.whenStable();
      TestBed.tick();
      await respond(page([]));

      expect(router.url).toBe('/');
      expect(element.textContent).toContain('No submissions yet.');
    });

    it('has a labelled control for every filter', async () => {
      await create();
      await respond(page([]));

      const labels = Array.from(element.querySelectorAll('mat-label')).map((l) =>
        l.textContent?.trim(),
      );
      expect(labels).toEqual(['Status', 'Cut-off from', 'Cut-off to']);
      expect(element.querySelector('mat-slide-toggle')?.textContent?.trim()).toBe('Current only');
    });
  });
});
