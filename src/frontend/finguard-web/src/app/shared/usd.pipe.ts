import { Pipe, PipeTransform } from '@angular/core';

const usd = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' });

/**
 * The only money formatter (D-208): en-US, USD, 2 decimals. Amounts arrive as JSON numbers, exact to the cent up to
 * about 9e13 USD (R-42).
 */
@Pipe({ name: 'usd' })
export class UsdPipe implements PipeTransform {
  transform(amount: number): string {
    return usd.format(amount);
  }
}
