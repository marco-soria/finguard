import { HttpClient, HttpParams, httpResource, HttpResourceRef } from '@angular/common/http';
import { inject, Injectable, Signal } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE } from './api-base';

/** A validation error of a rejected submission. `row` and `column` are null for file errors. */
export interface SubmissionError {
  row: number | null;
  column: string | null;
  code: string;
  message: string;
}

/** One row of `GET /api/v1/portfolio/submissions`. `cutOffDate` is a `yyyy-MM-dd` calendar date (D-103). */
export interface SubmissionListItem {
  submissionId: string;
  cutOffDate: string;
  fileName: string;
  sizeBytes: number;
  status: string;
  receivedAt: string;
  rowCount: number;
  errorCount: number;
  /** Latest accepted submission for its cut-off date; computed by the API (D-141). */
  isCurrent: boolean;
}

/** One page, newest first. `nextCursor` is null on the last page (D-299). */
export interface SubmissionList {
  items: SubmissionListItem[];
  nextCursor: string | null;
}

/**
 * History filters, all optional and combined by the API (D-304). Dates are `yyyy-MM-dd` strings (D-103); the API
 * validates every value and answers 400 with a `Portfolio.Submissions.*` code.
 */
export interface SubmissionFilters {
  status?: 'Accepted' | 'Rejected';
  cutOffFrom?: string;
  cutOffTo?: string;
  currentOnly?: boolean;
}

/** `GET /api/v1/portfolio/submissions/{id}`. `cutOffDate` is a `yyyy-MM-dd` calendar date (D-103). */
export interface SubmissionDetails {
  submissionId: string;
  cutOffDate: string;
  fileName: string;
  sizeBytes: number;
  status: string;
  receivedAt: string;
  rowCount: number;
  errorCount: number;
  /** Latest accepted submission for its cut-off date; computed by the API (D-141). */
  isCurrent: boolean;
  /** The first 100 errors, in row then column order. */
  errors: SubmissionError[];
}

/**
 * `POST /api/v1/portfolio/submissions`: 201 for a new `Accepted` or `Rejected` submission (D-112); 200 with
 * `duplicate: true` and the existing submission when the same file was already uploaded for the date (D-139).
 */
export interface SubmissionReceived {
  submissionId: string;
  status: string;
  receivedAt: string;
  rowCount: number;
  errorCount: number;
  errors: SubmissionError[];
  duplicate: boolean;
}

/**
 * Portfolio endpoints. The resource factories must run in an injection context: the resource then belongs to the
 * calling component and dies with it, so a signed-out user's data never outlives the page (D-102).
 */
@Injectable({ providedIn: 'root' })
export class PortfolioApi {
  private readonly http = inject(HttpClient);

  /**
   * One page of the history. `HttpClient`, not `httpResource`: "Load more" appends pages, which a resource cannot
   * accumulate. The calling component owns the subscription, so the data still dies with the page (D-102).
   */
  listSubmissions(filters: SubmissionFilters, cursor: string | null): Observable<SubmissionList> {
    let params = new HttpParams();
    if (filters.status) params = params.set('status', filters.status);
    if (filters.cutOffFrom) params = params.set('cutOffFrom', filters.cutOffFrom);
    if (filters.cutOffTo) params = params.set('cutOffTo', filters.cutOffTo);
    if (filters.currentOnly) params = params.set('currentOnly', 'true');
    if (cursor) params = params.set('cursor', cursor);
    return this.http.get<SubmissionList>(`${API_BASE}/portfolio/submissions`, { params });
  }

  submission(id: Signal<string>): HttpResourceRef<SubmissionDetails | undefined> {
    return httpResource<SubmissionDetails>(
      () => `${API_BASE}/portfolio/submissions/${encodeURIComponent(id())}`,
    );
  }

  upload(file: File, cutOffDate: string): Observable<SubmissionReceived> {
    // The browser sets the multipart Content-Type with its boundary; never set it by hand.
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('cutOffDate', cutOffDate);
    return this.http.post<SubmissionReceived>(`${API_BASE}/portfolio/submissions`, form);
  }
}
