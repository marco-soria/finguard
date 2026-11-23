import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import {
  ComplianceApi,
  EVALUATION_NOT_FOUND,
  EvaluationAlert,
} from '../../core/api/compliance-api';
import { PortfolioApi } from '../../core/api/portfolio-api';
import { ApiError } from '../../core/http/api-error';
import { errorMessage } from '../../core/http/problem-details.interceptor';
import { FileSizePipe } from '../../shared/file-size.pipe';
import { formatPercent, formatRatio } from '../../shared/format-ratio';
import { UsdPipe } from '../../shared/usd.pipe';

/** Waits before each retry while the evaluation is not ready (about 15 s in total, D-205). */
export const EVALUATION_RETRY_DELAYS_MS = [1000, 2000, 4000, 8000];

const CLASS_NAMES: Record<string, string> = {
  Cash: 'Cash',
  FixedIncome: 'Fixed income',
  Shares: 'Shares',
  RealEstate: 'Real estate',
};

const className = (assetClass: string) => CLASS_NAMES[assetClass] ?? assetClass;

const isNotEvaluatedYet = (error: unknown) =>
  error instanceof ApiError && error.status === 404 && error.code === EVALUATION_NOT_FOUND;

function alertText(alert: EvaluationAlert, alertThreshold: number): string {
  const used = formatRatio(alert.utilization, alert.status, alertThreshold);
  const where = alert.status === 'Breached' ? 'over' : 'near';
  return `${className(alert.assetClass)} is ${where} its limit (${used} of the limit used).`;
}

export type EvaluationState = 'none' | 'pending' | 'timeout' | 'error' | 'ready';

@Component({
  selector: 'app-submission-detail',
  imports: [DatePipe, FileSizePipe, MatButton, MatProgressBar, MatTableModule, RouterLink, UsdPipe],
  templateUrl: './submission-detail.html',
  styleUrl: './submission-detail.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export default class SubmissionDetail {
  /** The `:id` route parameter (router input binding). */
  readonly id = input.required<string>();

  protected readonly submission = inject(PortfolioApi).submission(this.id);
  protected readonly errorColumns = ['row', 'column', 'message'];
  protected readonly classColumns = [
    'assetClass',
    'bookValue',
    'share',
    'limit',
    'utilization',
    'status',
  ];
  protected readonly details = computed(() =>
    this.submission.hasValue() ? this.submission.value() : undefined,
  );
  protected readonly error = computed(() => {
    const error = this.submission.error();
    return error ? errorMessage(error) : null;
  });

  /** Only accepted submissions are evaluated; rejected ones never request it (D-204). */
  private readonly acceptedId = computed(() => {
    const d = this.details();
    return d?.status === 'Accepted' ? d.submissionId : undefined;
  });
  protected readonly evaluation = inject(ComplianceApi).evaluation(this.acceptedId);
  private readonly retries = signal(0);
  private retryTimer: ReturnType<typeof setTimeout> | undefined;

  protected readonly evaluationState = computed<EvaluationState>(() => {
    if (!this.acceptedId()) {
      return 'none';
    }
    if (this.evaluation.hasValue()) {
      return 'ready';
    }
    const error = this.evaluation.error();
    if (!error) {
      return 'pending';
    }
    if (isNotEvaluatedYet(error)) {
      return this.retries() < EVALUATION_RETRY_DELAYS_MS.length ? 'pending' : 'timeout';
    }
    return 'error';
  });
  protected readonly evaluationError = computed(() => errorMessage(this.evaluation.error()));

  /** The evaluation with display texts; statuses stay exactly as the API gave them. */
  protected readonly result = computed(() => {
    const e = this.evaluation.hasValue() ? this.evaluation.value() : undefined;
    if (!e) {
      return undefined;
    }
    return {
      ...e,
      rows: e.classes.map((c) => ({
        ...c,
        name: className(c.assetClass),
        shareText: formatPercent(c.share),
        limitText: formatPercent(c.limit),
        utilizationText: formatRatio(c.utilization, c.status, e.alertThreshold),
      })),
      alertTexts: e.alerts.map((a) => ({ status: a.status, text: alertText(a, e.alertThreshold) })),
    };
  });

  constructor() {
    // Each "not evaluated yet" answer schedules the next retry; success, another error or destroy ends it.
    effect(() => {
      if (isNotEvaluatedYet(this.evaluation.error())) {
        untracked(() => this.scheduleRetry());
      }
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(this.retryTimer));
  }

  protected checkAgain(): void {
    this.retries.set(0);
    this.evaluation.reload();
  }

  private scheduleRetry(): void {
    const delay = EVALUATION_RETRY_DELAYS_MS[this.retries()];
    if (delay === undefined) {
      return;
    }
    clearTimeout(this.retryTimer);
    this.retryTimer = setTimeout(() => {
      this.retries.update((n) => n + 1);
      this.evaluation.reload();
    }, delay);
  }
}
