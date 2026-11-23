const MIN_DECIMALS = 2;
const MAX_DECIMALS = 8;

/** A ratio as a percentage with 2 decimals (`0.25` -> `25.00 %`), for shares and limits. */
export function formatPercent(value: number): string {
  return `${(value * 100).toFixed(MIN_DECIMALS)} %`;
}

/**
 * A utilization as a percentage that never contradicts the status the API gave (D-207, R-39). Starts with 2 decimals
 * and adds decimals (max 8) while the text would read as another status: `0.89999996` Compliant is `89.999996 %`,
 * never `90.00 %`. It only formats; the status always comes from the API.
 */
export function formatRatio(value: number, status: string, alertThreshold: number): string {
  // toFixed strips the binary noise of `0.9 * 100` (90.00000000000001), so 90.00 % agrees with Warning.
  const threshold = Number((alertThreshold * 100).toFixed(MAX_DECIMALS));
  let text = '';
  for (let decimals = MIN_DECIMALS; decimals <= MAX_DECIMALS; decimals++) {
    text = (value * 100).toFixed(decimals);
    if (agrees(Number(text), status, threshold)) {
      break;
    }
  }
  return `${text} %`;
}

function agrees(shown: number, status: string, threshold: number): boolean {
  switch (status) {
    case 'Compliant':
      return shown < threshold;
    case 'Warning':
      return shown >= threshold && shown <= 100;
    case 'Breached':
      return shown > 100;
    default:
      return true;
  }
}
