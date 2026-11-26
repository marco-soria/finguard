import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import {
  ComplianceApi,
  EVALUATION_NOT_FOUND,
  EvaluationAlert,
  EvaluationResponse,
} from '../../core/api/compliance-api';
import { PortfolioApi } from '../../core/api/portfolio-api';
import { errorMessage } from '../../core/http/problem-details.interceptor';
import { FileSizePipe } from '../../shared/file-size.pipe';
import { formatPercent, formatRatio } from '../../shared/format-ratio';
import { retryWhileNotFound } from '../../shared/retry-while-not-found';
import { UsdPipe } from '../../shared/usd.pipe';
import { AdvisoryPanel } from './advisory-panel';

const CLASS_NAMES: Record<string, string> = {
  Cash: 'Cash',
  FixedIncome: 'Fixed income',
  Shares: 'Shares',
  RealEstate: 'Real estate',
};

const className = (assetClass: string) => CLASS_NAMES[assetClass] ?? assetClass;

function alertText(alert: EvaluationAlert, alertThreshold: number): string {
  const used = formatRatio(alert.utilization, alert.status, alertThreshold);
  const where = alert.status === 'Breached' ? 'over' : 'near';
  return `${className(alert.assetClass)} is ${where} its limit (${used} of the limit used).`;
}

const usd = new UsdPipe();

const PROPOSAL_SENTENCES: Record<string, string> = {
  NotNeeded: 'No rebalancing needed.',
  Full: 'Moving the amounts below brings every class within its limit.',
  // The API does not say which class stays outside its target, so the sentence does not name one.
  Partial: 'Partial: rounding leaves a class outside its target; review the amounts.',
};

/** An explicit sign and a word, never colour alone (D-209). The API's amounts are exact to the cent. */
export function changeText(change: number): string {
  const cents = Math.round(change * 100) / 100;
  const amount = usd.transform(Math.abs(cents));
  if (cents > 0) {
    return `+${amount} (increase)`;
  }
  return cents < 0 ? `−${amount} (decrease)` : `${amount} (no change)`;
}

function proposalView(e: EvaluationResponse) {
  if (!e.proposal) {
    return null;
  }
  const changeTotal = e.proposal.moves.reduce((sum, m) => sum + m.change, 0);
  return {
    status: e.proposal.status,
    sentence: PROPOSAL_SENTENCES[e.proposal.status] ?? e.proposal.status,
    rows: e.proposal.moves.map((m) => ({
      ...m,
      name: className(m.assetClass),
      changeText: changeText(m.change),
    })),
    currentTotal: e.totalBookValue,
    proposedTotal: e.totalBookValue + changeTotal,
    changeTotalText: changeText(changeTotal),
  };
}

export type EvaluationState = 'none' | 'pending' | 'timeout' | 'error' | 'ready';

@Component({
  selector: 'app-submission-detail',
  imports: [
    AdvisoryPanel,
    DatePipe,
    FileSizePipe,
    MatButton,
    MatProgressBar,
    MatTableModule,
    RouterLink,
    UsdPipe,
  ],
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
  protected readonly proposalColumns = ['assetClass', 'current', 'proposed', 'change'];
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
  private readonly retry = retryWhileNotFound(this.evaluation, EVALUATION_NOT_FOUND);

  protected readonly evaluationState = computed<EvaluationState>(() =>
    this.acceptedId() ? this.retry.state() : 'none',
  );
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
      proposal: proposalView(e),
    };
  });

  protected checkAgain(): void {
    this.retry.checkAgain();
  }
}
