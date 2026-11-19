import { Pipe, PipeTransform } from '@angular/core';

const KB = 1024;
const MB = KB * 1024;

/** Bytes as `B`, `KB` or `MB` with one decimal (binary units, matching the API's 5 MB limit). */
@Pipe({ name: 'fileSize' })
export class FileSizePipe implements PipeTransform {
  transform(bytes: number): string {
    if (bytes < KB) {
      return `${bytes} B`;
    }
    return bytes < MB ? `${(bytes / KB).toFixed(1)} KB` : `${(bytes / MB).toFixed(1)} MB`;
  }
}
