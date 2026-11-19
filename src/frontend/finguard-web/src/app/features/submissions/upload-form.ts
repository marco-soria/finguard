import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  FormGroupDirective,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { finalize } from 'rxjs';
import { PortfolioApi, SubmissionReceived } from '../../core/api/portfolio-api';
import { errorTitle } from '../../core/http/problem-details.interceptor';

export const MAX_FILE_BYTES = 5 * 1024 * 1024;

/** Input constraints for fast feedback only; the API stays the authority (D-104). */
function portfolioFile(control: AbstractControl<File | null>): ValidationErrors | null {
  const file = control.value;
  if (!file) {
    return null;
  }
  if (!file.name.toLowerCase().endsWith('.csv')) {
    return { notCsv: true };
  }
  if (file.size === 0) {
    return { empty: true };
  }
  return file.size > MAX_FILE_BYTES ? { tooLarge: true } : null;
}

@Component({
  selector: 'app-upload-form',
  imports: [ReactiveFormsModule, MatButton, MatError, MatFormField, MatInput, MatLabel],
  templateUrl: './upload-form.html',
  styleUrl: './upload-form.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UploadForm {
  private readonly api = inject(PortfolioApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fileInput = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');
  private readonly formDirective = viewChild.required(FormGroupDirective);

  readonly uploaded = output<SubmissionReceived>();

  protected readonly form = new FormGroup({
    cutOffDate: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    file: new FormControl<File | null>(null, [Validators.required, portfolioFile]),
  });
  protected readonly sending = signal(false);
  protected readonly status = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  // A native file input has no ControlValueAccessor, so the control is fed from the change event.
  protected fileChosen(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.item(0) ?? null;
    this.form.controls.file.setValue(file);
    this.form.controls.file.markAsTouched();
  }

  protected submit(): void {
    if (this.sending()) {
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { cutOffDate, file } = this.form.getRawValue();
    if (!file) {
      return;
    }
    this.sending.set(true);
    this.status.set(null);
    this.error.set(null);
    this.api
      .upload(file, cutOffDate)
      .pipe(
        finalize(() => this.sending.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (received) => {
          this.status.set(`Uploaded ${file.name}.`);
          // resetForm, not form.reset: it also clears `submitted`, which Material uses to show field errors.
          this.formDirective().resetForm();
          this.fileInput().nativeElement.value = '';
          this.uploaded.emit(received);
        },
        error: (error: unknown) => this.error.set(errorTitle(error)),
      });
  }
}
