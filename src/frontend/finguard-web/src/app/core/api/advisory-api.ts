import { httpResource, HttpResourceRef } from '@angular/common/http';
import { Injectable, Signal } from '@angular/core';
import { API_BASE } from './api-base';

/**
 * `GET /api/v1/advisory/submissions/{id}/advisory` (D-256). `paragraphs` are plain text: render them by interpolation
 * only, never as HTML (D-253).
 */
export interface AdvisoryResponse {
  evaluationId: string;
  submissionId: string;
  /** Which narrator wrote the text: always `Template` (D-293). Any other value gets no AI context. */
  narrator: string;
  generatedAt: string;
  paragraphs: string[];
  /** The model that wrote `aiContext`; null otherwise (ADR-0015). */
  model: string | null;
  refinement: AdvisoryRefinement;
  /** One plain-text paragraph with no figures or statuses (D-293); null unless accepted. Interpolation only. */
  aiContext: string | null;
}

/** Whether an AI context paragraph was requested and how it ended (ADR-0015, D-293). */
export type AdvisoryRefinement = 'NotRequested' | 'Pending' | 'Accepted' | 'Rejected';

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
      return id ? `${API_BASE}/advisory/submissions/${encodeURIComponent(id)}/advisory` : undefined;
    });
  }
}
