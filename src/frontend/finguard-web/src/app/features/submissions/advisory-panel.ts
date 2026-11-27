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
import { ADVISORY_NOT_FOUND, AdvisoryApi, AdvisoryResponse } from '../../core/api/advisory-api';
import { errorMessage } from '../../core/http/problem-details.interceptor';
import { NOT_FOUND_RETRY_DELAYS_MS, retryWhileNotFound } from '../../shared/retry-while-not-found';

export type AdvisoryState = 'none' | 'pending' | 'timeout' | 'error' | 'ready';

export const REFINEMENT_PENDING_TEXT = 'An AI-written version is being prepared…';

/** Which narrator wrote the text (D-288). Model text is shown only after it passed validation (ADR-0015). */
export function sourceLine(advisory: AdvisoryResponse): string | null {
  switch (advisory.narrator) {
    case 'Template':
      return 'Written from a template.';
    case 'Model':
      return `Written by an AI model (${advisory.model ?? 'unknown'}) and checked against the evaluation's numbers.`;
    default:
      return null;
  }
}

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
  /** Reloads made while the model rewrite is pending; the polling stops silently after the last delay. */
  private readonly refinementPolls = signal(0);

  private readonly value = computed(() =>
    this.advisory.hasValue() ? this.advisory.value() : undefined,
  );
  protected readonly state = computed<AdvisoryState>(() =>
    this.hasProposal() ? this.retry.state() : 'none',
  );
  protected readonly paragraphs = computed(() => this.value()?.paragraphs ?? []);
  protected readonly source = computed(() => {
    const advisory = this.value();
    return advisory ? sourceLine(advisory) : null;
  });
  /** The template is already a complete answer: this note only says a model version may replace it. */
  protected readonly refining = computed(
    () =>
      this.value()?.refinement === 'Pending' &&
      this.refinementPolls() < NOT_FOUND_RETRY_DELAYS_MS.length,
  );
  protected readonly errorText = computed(() => errorMessage(this.advisory.error()));
  protected readonly refiningText = REFINEMENT_PENDING_TEXT;

  constructor() {
    let timer: ReturnType<typeof setTimeout> | undefined;
    // Each pending answer schedules the next reload with the shared delays (D-288).
    effect(() => {
      if (this.value()?.refinement !== 'Pending' || this.advisory.isLoading()) {
        return;
      }
      const delay = NOT_FOUND_RETRY_DELAYS_MS[untracked(this.refinementPolls)];
      if (delay === undefined) {
        return;
      }
      clearTimeout(timer);
      timer = setTimeout(() => {
        this.refinementPolls.update((n) => n + 1);
        this.advisory.reload();
      }, delay);
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(timer));
  }

  protected checkAgain(): void {
    this.retry.checkAgain();
  }
}
