import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';

@Component({
  selector: 'kadr-avatar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (blobUrl()) {
      <img
        [src]="blobUrl()!"
        alt=""
        class="shrink-0 rounded-full object-cover"
        [class]="sizeClass()"
        [attr.aria-hidden]="ariaLabel() ? null : true"
        [attr.aria-label]="ariaLabel() || null"
        role="img"
      />
    } @else {
      <span
        class="inline-flex shrink-0 items-center justify-center rounded-full bg-accent-soft font-medium text-accent-soft-fg"
        [class]="sizeClass()"
        [attr.aria-hidden]="ariaLabel() ? null : true"
        [attr.aria-label]="ariaLabel() || null"
        role="img"
      >
        {{ initials() }}
      </span>
    }
  `,
})
export class AvatarComponent {
  private readonly http = inject(HttpClient);
  private objectUrl: string | null = null;

  readonly name = input.required<string>();
  /** Relative API path, e.g. /api/v1/files/{guid}. */
  readonly imageUrl = input<string | null | undefined>(undefined);
  readonly ariaLabel = input<string | undefined>(undefined);
  readonly size = input<'sm' | 'md' | 'lg'>('md');

  protected readonly blobUrl = signal<string | null>(null);

  protected readonly initials = computed(() => {
    const parts = this.name().trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) {
      return '?';
    }
    if (parts.length === 1) {
      return parts[0]!.slice(0, 2).toUpperCase();
    }
    return `${parts[0]![0] ?? ''}${parts[1]![0] ?? ''}`.toUpperCase();
  });

  protected readonly sizeClass = computed(() => {
    switch (this.size()) {
      case 'sm':
        return 'size-8 text-xs';
      case 'lg':
        return 'size-12 text-base';
      default:
        return 'size-10 text-sm';
    }
  });

  constructor() {
    effect((onCleanup) => {
      const path = this.imageUrl();
      this.revoke();
      if (!path) {
        this.blobUrl.set(null);
        return;
      }

      const sub = this.http
        .get(path, { responseType: 'blob', withCredentials: true })
        .subscribe({
          next: (blob) => {
            this.revoke();
            this.objectUrl = URL.createObjectURL(blob);
            this.blobUrl.set(this.objectUrl);
          },
          error: () => {
            this.revoke();
            this.blobUrl.set(null);
          },
        });

      onCleanup(() => {
        sub.unsubscribe();
        this.revoke();
      });
    });
  }

  private revoke(): void {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
  }
}
