import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { PortfolioApi } from '../../core/api/portfolio-api';
import { errorMessage } from '../../core/http/problem-details.interceptor';
import { FileSizePipe } from '../../shared/file-size.pipe';

@Component({
  selector: 'app-submission-detail',
  imports: [DatePipe, FileSizePipe, MatProgressBar, MatTableModule, RouterLink],
  templateUrl: './submission-detail.html',
  styleUrl: './submission-detail.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export default class SubmissionDetail {
  /** The `:id` route parameter (router input binding). */
  readonly id = input.required<string>();

  protected readonly submission = inject(PortfolioApi).submission(this.id);
  protected readonly errorColumns = ['row', 'column', 'message'];
  protected readonly details = computed(() =>
    this.submission.hasValue() ? this.submission.value() : undefined,
  );
  protected readonly error = computed(() => {
    const error = this.submission.error();
    return error ? errorMessage(error) : null;
  });
}
