import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AvatarComponent } from '../avatar/avatar.component';

/** Loads a tenant logo URL with auth (Bearer) into a blob URL for img display. */
@Component({
  selector: 'kadr-tenant-logo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AvatarComponent],
  template: `
    @if (blobUrl()) {
      <img
        [src]="blobUrl()!"
        alt=""
        class="object-contain"
        [class]="sizeClass()"
      />
    } @else if (fallbackName()) {
      <kadr-avatar [name]="fallbackName()!" [size]="avatarSize()" [ariaLabel]="fallbackName()!" />
    } @else {
      <img
        src="/brand/logo-kadr.png"
        alt=""
        class="object-contain"
        [class]="sizeClass()"
      />
    }
  `,
})
export class TenantLogoComponent {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);

  readonly logoUrl = input<string | null | undefined>(undefined);
  readonly fallbackName = input<string | null | undefined>(undefined);
  readonly size = input<'sm' | 'md' | 'lg'>('md');

  protected readonly blobUrl = signal<string | null>(null);

  constructor() {
    effect((onCleanup) => {
      const url = this.logoUrl();
      this.revoke();
      this.blobUrl.set(null);
      if (!url) {
        return;
      }

      const sub = this.http
        .get(url, { responseType: 'blob', withCredentials: true })
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (blob) => {
            const objectUrl = URL.createObjectURL(blob);
            this.blobUrl.set(objectUrl);
          },
          error: () => this.blobUrl.set(null),
        });

      onCleanup(() => {
        sub.unsubscribe();
        this.revoke();
      });
    });
  }

  protected sizeClass(): string {
    switch (this.size()) {
      case 'sm':
        return 'size-8';
      case 'lg':
        return 'size-14';
      default:
        return 'size-9';
    }
  }

  protected avatarSize(): 'sm' | 'md' | 'lg' {
    return this.size();
  }

  private revoke(): void {
    const current = this.blobUrl();
    if (current) {
      URL.revokeObjectURL(current);
    }
  }
}
