import { httpResource, HttpResourceRef } from '@angular/common/http';
import { Injectable, Signal } from '@angular/core';

/**
 * `GET /api/advisory/submissions/{id}/advisory` (D-256). `paragraphs` are plain text: render them by interpolation
 * only, never as HTML (D-253).
 */
export interface AdvisoryResponse {
  evaluationId: string;
  submissionId: string;
  /** Which narrator wrote the text, for example `Template`. */
  narrator: string;
  generatedAt: string;
  paragraphs: string[];
}

/** The API's 404 code for an unknown, foreign or not-yet-written advisory (D-256). */
export const ADVISORY_NOT_FOUND = 'Advisory.Advisory.NotFound';

/** Advisory endpoints. Resource factories must run in an injection context (D-102). */
@Injectable({ providedIn: 'root' })
export class AdvisoryApi {
  /** No request while `submissionId` is undefined. */
  advisory(
    submissionId: Signal<string | undefined>,
  ): HttpResourceRef<AdvisoryResponse | undefined> {
    return httpResource<AdvisoryResponse>(() => {
      const id = submissionId();
      return id ? `/api/advisory/submissions/${encodeURIComponent(id)}/advisory` : undefined;
    });
  }
}
