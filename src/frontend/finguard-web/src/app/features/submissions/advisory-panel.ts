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

export const REFINEMENT_PENDING_TEXT = 'Adding AI context…';

/** The narrative is always the template's (D-293); the label never depends on the narrator value. */
export const TEMPLATE_SOURCE_TEXT = 'Written from a template.';

export interface AiContext {
  text: string;
  label: string;
}

/**
 * The validated AI context paragraph, or null (D-293). Fails closed: only beside a `Template` narrative, only when
 * accepted, and only with a text and a model.
 */
export function aiContextOf(advisory: AdvisoryResponse): AiContext | null {
  const { narrator, refinement, aiContext, model } = advisory;
  if (narrator !== 'Template' || refinement !== 'Accepted' || !aiContext || !model) {
    return null;
  }
  return {
    text: aiContext,
    label: `Written by an AI model (${model}). It contains no figures or statuses; all figures above come from the evaluation.`,
  };
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
  /** Reloads made while the AI context is pending; the polling stops silently after the last delay. */
  private readonly refinementPolls = signal(0);

  private readonly value = computed(() =>
    this.advisory.hasValue() ? this.advisory.value() : undefined,
  );
  protected readonly state = computed<AdvisoryState>(() =>
    this.hasProposal() ? this.retry.state() : 'none',
  );
  protected readonly paragraphs = computed(() => this.value()?.paragraphs ?? []);
  protected readonly source = TEMPLATE_SOURCE_TEXT;
  protected readonly context = computed(() => {
    const advisory = this.value();
    return advisory ? aiContextOf(advisory) : null;
  });
  /** The template is already a complete answer: this note only says a context paragraph may follow. */
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
