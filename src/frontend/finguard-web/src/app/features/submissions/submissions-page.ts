import { ChangeDetectionStrategy, Component, viewChild } from '@angular/core';
import { SubmissionHistory } from './submission-history';
import { UploadForm } from './upload-form';

@Component({
  selector: 'app-submissions-page',
  imports: [SubmissionHistory, UploadForm],
  template: `
    <app-upload-form (uploaded)="refresh()" />
    <app-submission-history />
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 2rem;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export default class SubmissionsPage {
  private readonly history = viewChild.required(SubmissionHistory);

  protected refresh(): void {
    this.history().reload();
  }
}
