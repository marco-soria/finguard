/** The year the project started; the footer never shows an earlier one. */
export const FIRST_YEAR = 2026;

/** `2026` in 2026, `2026–{year}` afterwards (D-289). */
export function copyrightYears(currentYear: number): string {
  return currentYear > FIRST_YEAR ? `${FIRST_YEAR}–${currentYear}` : `${FIRST_YEAR}`;
}
