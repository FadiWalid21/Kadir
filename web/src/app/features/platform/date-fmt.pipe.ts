import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'dateFmt', pure: true })
export class DateFmtPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) {
      return '—';
    }
    try {
      return new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value));
    } catch {
      return value;
    }
  }
}
