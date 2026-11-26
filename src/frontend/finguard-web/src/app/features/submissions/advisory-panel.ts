import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { ADVISORY_NOT_FOUND, AdvisoryApi } from '../../core/api/advisory-api';
import { errorMessage } from '../../core/http/problem-details.interceptor';
import { retryWhileNotFound } from '../../shared/retry-while-not-found';

export type AdvisoryState = 'none' | 'pending' | 'timeout' | 'error' | 'ready';

const SOURCE_LINES: Record<string, string> = {
  Template: 'Written from a template.',
};

/** The narrative of an evaluation (D-257). Paragraphs are plain text, shown by interpolation only (D-253). */
@Component({
  selector: 'app-advisory-panel',
  imports: [MatButton],
  templateUrl: './advisory-panel.html',
  styleUrl: './advisory-panel.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdvisoryPanel {
  readonly submissionId = input.required<string>();
  /** False for evaluations stored before the rebalance engine: they have no advisory and get no request. */
  readonly hasProposal = input.required<boolean>();

  private readonly requestId = computed(() =>
    this.hasProposal() ? this.submissionId() : undefined,
  );
  protected readonly advisory = inject(AdvisoryApi).advisory(this.requestId);
  private readonly retry = retryWhileNotFound(this.advisory, ADVISORY_NOT_FOUND);

  protected readonly state = computed<AdvisoryState>(() =>
    this.hasProposal() ? this.retry.state() : 'none',
  );
  protected readonly paragraphs = computed(() =>
    this.advisory.hasValue() ? (this.advisory.value()?.paragraphs ?? []) : [],
  );
  protected readonly source = computed(() => {
    const narrator = this.advisory.hasValue() ? this.advisory.value()?.narrator : undefined;
    return narrator ? (SOURCE_LINES[narrator] ?? null) : null;
  });
  protected readonly errorText = computed(() => errorMessage(this.advisory.error()));

  protected checkAgain(): void {
    this.retry.checkAgain();
  }
}
