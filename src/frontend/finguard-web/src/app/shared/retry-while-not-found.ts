import { HttpResourceRef } from '@angular/common/http';
import { computed, DestroyRef, effect, inject, Signal, signal, untracked } from '@angular/core';
import { ApiError } from '../core/http/api-error';

/** Waits before each retry while the API answers "not ready yet" (about 15 s in total, D-205). */
export const NOT_FOUND_RETRY_DELAYS_MS = [1000, 2000, 4000, 8000];

/** `pending` covers the first request and every retry; `timeout` waits for "Check again". */
export type RetryState = 'pending' | 'timeout' | 'error' | 'ready';

export interface NotFoundRetry {
  readonly state: Signal<RetryState>;
  /** Starts the retry loop over (the "Check again" button). */
  checkAgain(): void;
}

/**
 * Reloads `resource` while it fails with a 404 carrying `notFoundCode`, waiting `NOT_FOUND_RETRY_DELAYS_MS` between
 * attempts (D-257). Success, another error or destroying the caller ends the loop. Must run in an injection context.
 */
export function retryWhileNotFound(
  resource: HttpResourceRef<unknown>,
  notFoundCode: string,
): NotFoundRetry {
  const retries = signal(0);
  let timer: ReturnType<typeof setTimeout> | undefined;
  const isNotFound = (error: unknown) =>
    error instanceof ApiError && error.status === 404 && error.code === notFoundCode;

  const state = computed<RetryState>(() => {
    if (resource.hasValue()) {
      return 'ready';
    }
    const error = resource.error();
    if (!error) {
      return 'pending';
    }
    if (isNotFound(error)) {
      return retries() < NOT_FOUND_RETRY_DELAYS_MS.length ? 'pending' : 'timeout';
    }
    return 'error';
  });

  const scheduleRetry = () => {
    const delay = NOT_FOUND_RETRY_DELAYS_MS[retries()];
    if (delay === undefined) {
      return;
    }
    clearTimeout(timer);
    timer = setTimeout(() => {
      retries.update((n) => n + 1);
      resource.reload();
    }, delay);
  };

  // Each "not ready yet" answer schedules the next retry.
  effect(() => {
    if (isNotFound(resource.error())) {
      untracked(scheduleRetry);
    }
  });
  inject(DestroyRef).onDestroy(() => clearTimeout(timer));

  return {
    state,
    checkAgain: () => {
      retries.set(0);
      resource.reload();
    },
  };
}
