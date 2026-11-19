import { HttpClient, httpResource, HttpResourceRef } from '@angular/common/http';
import { inject, Injectable, Signal } from '@angular/core';
import { Observable } from 'rxjs';

/** A validation error of a rejected submission. `row` and `column` are null for file errors. */
export interface SubmissionError {
  row: number | null;
  column: string | null;
  code: string;
  message: string;
}

/** One row of `GET /api/portfolio/submissions`. `cutOffDate` is a `yyyy-MM-dd` calendar date (D-103). */
export interface SubmissionListItem {
  submissionId: string;
  cutOffDate: string;
  fileName: string;
  sizeBytes: number;
  status: string;
  receivedAt: string;
  rowCount: number;
  errorCount: number;
}

export interface SubmissionList {
  items: SubmissionListItem[];
}

/** `GET /api/portfolio/submissions/{id}`. `cutOffDate` is a `yyyy-MM-dd` calendar date (D-103). */
export interface SubmissionDetails {
  submissionId: string;
  cutOffDate: string;
  fileName: string;
  sizeBytes: number;
  status: string;
  receivedAt: string;
  rowCount: number;
  errorCount: number;
  /** The first 100 errors, in row then column order. */
  errors: SubmissionError[];
}

/** `POST /api/portfolio/submissions`: 201 for both `Accepted` and `Rejected` (D-112). */
export interface SubmissionReceived {
  submissionId: string;
  status: string;
  receivedAt: string;
  rowCount: number;
  errorCount: number;
  errors: SubmissionError[];
}

/**
 * Portfolio endpoints. The resource factories must run in an injection context: the resource then belongs to the
 * calling component and dies with it, so a signed-out user's data never outlives the page (D-102).
 */
@Injectable({ providedIn: 'root' })
export class PortfolioApi {
  private readonly http = inject(HttpClient);

  submissions(): HttpResourceRef<SubmissionList | undefined> {
    return httpResource<SubmissionList>(() => '/api/portfolio/submissions');
  }

  submission(id: Signal<string>): HttpResourceRef<SubmissionDetails | undefined> {
    return httpResource<SubmissionDetails>(
      () => `/api/portfolio/submissions/${encodeURIComponent(id())}`,
    );
  }

  upload(file: File, cutOffDate: string): Observable<SubmissionReceived> {
    // The browser sets the multipart Content-Type with its boundary; never set it by hand.
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('cutOffDate', cutOffDate);
    return this.http.post<SubmissionReceived>('/api/portfolio/submissions', form);
  }
}
