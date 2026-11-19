import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatProgressBar } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { PortfolioApi } from '../../core/api/portfolio-api';
import { errorTitle } from '../../core/http/problem-details.interceptor';
import { FileSizePipe } from '../../shared/file-size.pipe';

@Component({
  selector: 'app-submission-detail',
  imports: [DatePipe, FileSizePipe, MatProgressBar, RouterLink],
  templateUrl: './submission-detail.html',
  styleUrl: './submission-detail.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export default class SubmissionDetail {
  /** The `:id` route parameter (router input binding). */
  readonly id = input.required<string>();

  protected readonly submission = inject(PortfolioApi).submission(this.id);
  protected readonly details = computed(() =>
    this.submission.hasValue() ? this.submission.value() : undefined,
  );
  protected readonly error = computed(() => {
    const error = this.submission.error();
    return error ? errorTitle(error) : null;
  });
}
