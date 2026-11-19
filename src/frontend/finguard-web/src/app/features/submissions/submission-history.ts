import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { PortfolioApi, SubmissionListItem } from '../../core/api/portfolio-api';
import { errorTitle } from '../../core/http/problem-details.interceptor';
import { FileSizePipe } from '../../shared/file-size.pipe';

@Component({
  selector: 'app-submission-history',
  imports: [DatePipe, FileSizePipe, MatProgressBar, MatTableModule, RouterLink],
  templateUrl: './submission-history.html',
  styleUrl: './submission-history.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubmissionHistory {
  protected readonly submissions = inject(PortfolioApi).submissions();
  protected readonly columns = ['cutOffDate', 'fileName', 'sizeBytes', 'status', 'receivedAt'];
  protected readonly items = computed<SubmissionListItem[] | undefined>(() =>
    this.submissions.hasValue() ? this.submissions.value()?.items : undefined,
  );
  protected readonly error = computed(() => {
    const error = this.submissions.error();
    return error ? errorTitle(error) : null;
  });

  reload(): void {
    this.submissions.reload();
  }
}
