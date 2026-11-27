import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatButton } from '@angular/material/button';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatOption, MatSelect } from '@angular/material/select';
import { MatSlideToggle } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { catchError, EMPTY, map, Subject, switchMap, tap } from 'rxjs';
import {
  PortfolioApi,
  SubmissionFilters,
  SubmissionList,
  SubmissionListItem,
} from '../../core/api/portfolio-api';
import { errorMessage } from '../../core/http/problem-details.interceptor';
import { FileSizePipe } from '../../shared/file-size.pipe';

/** Query-string keys, the same names the API uses, so a shared URL reproduces the same list. */
const FILTER_KEYS = ['status', 'cutOffFrom', 'cutOffTo', 'currentOnly'] as const;

/** Reads the filters from the URL. Unknown values are passed on as-is: the API validates them (no rules here). */
export function filtersFromParams(params: ParamMap): SubmissionFilters {
  const filters: SubmissionFilters = {};
  const status = params.get('status');
  if (status) filters.status = status as SubmissionFilters['status'];
  const from = params.get('cutOffFrom');
  if (from) filters.cutOffFrom = from;
  const to = params.get('cutOffTo');
  if (to) filters.cutOffTo = to;
  if (params.get('currentOnly') === 'true') filters.currentOnly = true;
  return filters;
}

interface PageRequest {
  filters: SubmissionFilters;
  cursor: string | null;
}

@Component({
  selector: 'app-submission-history',
  imports: [
    DatePipe,
    FileSizePipe,
    MatButton,
    MatFormField,
    MatInput,
    MatLabel,
    MatOption,
    MatProgressBar,
    MatSelect,
    MatSlideToggle,
    MatTableModule,
    RouterLink,
  ],
  templateUrl: './submission-history.html',
  styleUrl: './submission-history.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubmissionHistory {
  private readonly api = inject(PortfolioApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly requests = new Subject<PageRequest>();

  protected readonly columns = [
    'cutOffDate',
    'fileName',
    'sizeBytes',
    'status',
    'errorCount',
    'receivedAt',
  ];

  /** The filters in the URL are the source of truth (D-304); controls only navigate. */
  protected readonly filters = toSignal(this.route.queryParamMap.pipe(map(filtersFromParams)), {
    requireSync: true,
  });
  protected readonly filtered = computed(() => Object.keys(this.filters()).length > 0);

  /** Every page loaded so far, in API order; undefined until the first page arrives. */
  protected readonly items = signal<SubmissionListItem[] | undefined>(undefined);
  protected readonly nextCursor = signal<string | null>(null);
  protected readonly loading = signal(false);
  private readonly failure = signal<unknown>(null);
  protected readonly error = computed(() => {
    const error = this.failure();
    return error ? errorMessage(error) : null;
  });

  constructor() {
    // switchMap: a new filter or a reload cancels a page still in flight, so pages never mix.
    this.requests
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.failure.set(null);
        }),
        switchMap((request) =>
          this.api.listSubmissions(request.filters, request.cursor).pipe(
            map((page) => ({ request, page })),
            // Caught inside: an error must not end the stream, or later filters would load nothing.
            catchError((error: unknown) => {
              this.loading.set(false);
              this.failure.set(error);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe({ next: ({ request, page }) => this.show(request, page) });

    this.route.queryParamMap
      .pipe(takeUntilDestroyed())
      .subscribe((params) => this.firstPage(filtersFromParams(params)));
  }

  /** Back to the first page with the current filters, for example after an upload. */
  reload(): void {
    this.firstPage(this.filters());
  }

  protected loadMore(): void {
    const cursor = this.nextCursor();
    if (cursor && !this.loading()) {
      this.requests.next({ filters: this.filters(), cursor });
    }
  }

  protected setFilter(key: (typeof FILTER_KEYS)[number], value: string | boolean | null): void {
    // Empty values leave the URL; currentOnly is only written when on.
    const param = value === '' || value === false || value === null ? null : String(value);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { [key]: param },
      queryParamsHandling: 'merge',
    });
  }

  protected clearFilters(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: Object.fromEntries(FILTER_KEYS.map((key) => [key, null])),
      queryParamsHandling: 'merge',
    });
  }

  private firstPage(filters: SubmissionFilters): void {
    this.items.set(undefined);
    this.nextCursor.set(null);
    this.requests.next({ filters, cursor: null });
  }

  private show(request: PageRequest, page: SubmissionList): void {
    this.items.update((items) =>
      request.cursor && items ? [...items, ...page.items] : page.items,
    );
    this.nextCursor.set(page.nextCursor);
    this.loading.set(false);
  }
}
