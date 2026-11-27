import { ChangeDetectionStrategy, Component } from '@angular/core';
import { copyrightYears } from './copyright-years';

export const REPOSITORY_URL = 'https://github.com/marco-soria/finguard';
export const LICENSE_URL = `${REPOSITORY_URL}/blob/main/LICENSE`;

/** One line of credits and a demo notice on every page, including /login (D-289). */
@Component({
  selector: 'app-site-footer',
  templateUrl: './site-footer.html',
  styleUrl: './site-footer.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteFooter {
  /** Computed once at startup from the browser clock. */
  protected readonly years = copyrightYears(new Date().getFullYear());
  protected readonly licenseUrl = LICENSE_URL;
  protected readonly repositoryUrl = REPOSITORY_URL;
}
