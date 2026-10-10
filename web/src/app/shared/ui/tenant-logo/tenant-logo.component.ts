import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { AvatarComponent } from '../avatar/avatar.component';

type LogoView = 'loading' | 'image' | 'fallback';

/** Loads a tenant logo via authenticated HttpClient (blob → object URL). */
@Component({
  selector: 'kadr-tenant-logo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AvatarComponent],
  template: `
    @if (view() === 'image' && blobUrl()) {
      <img [src]="blobUrl()!" alt="" class="object-contain" [class]="sizeClass()" />
    } @else if (fallbackName()) {
      <kadr-avatar [name]="fallbackName()!" [size]="avatarSize()" [ariaLabel]="fallbackName()!" />
    } @else {
      <img src="/brand/logo-kadr.png" alt="" class="object-contain" [class]="sizeClass()" />
    }
  `,
})
export class TenantLogoComponent {
  private readonly http = inject(HttpClient);
  /** Kept off the signal graph so revoke/clear does not re-trigger the load effect. */
  private objectUrl: string | null = null;

  /** Relative API path, e.g. /api/v1/files/{guid}. */
  readonly logoUrl = input<string | null | undefined>(undefined);
  /** Bump after upload/replace/remove so the image reloads (number or file id string). */
  readonly logoVersion = input<string | number>(0);
  readonly fallbackName = input<string | null | undefined>(undefined);
  readonly size = input<'sm' | 'md' | 'lg'>('md');

  protected readonly blobUrl = signal<string | null>(null);
  protected readonly view = signal<LogoView>('fallback');

  constructor() {
    effect((onCleanup) => {
      const path = this.logoUrl();
      const version = this.logoVersion();
      this.revokeObjectUrl();

      if (!path) {
        this.blobUrl.set(null);
        this.view.set('fallback');
        return;
      }

      this.blobUrl.set(null);
      this.view.set('loading');
      const requestUrl = this.withCacheBust(path, version);

      const sub = this.http
        .get(requestUrl, { responseType: 'blob', observe: 'response', withCredentials: true })
        .subscribe({
          next: (res) => {
            const blob = res.body;
            if (!blob || blob.size === 0) {
              this.view.set('fallback');
              return;
            }
            const headerType = res.headers.get('Content-Type');
            if (this.isRejectedContent(blob, headerType)) {
              this.view.set('fallback');
              return;
            }
            const created = URL.createObjectURL(blob);
            this.objectUrl = created;
            this.blobUrl.set(created);
            this.view.set('image');
          },
          error: () => {
            this.view.set('fallback');
          },
        });

      onCleanup(() => {
        sub.unsubscribe();
        this.revokeObjectUrl();
        this.blobUrl.set(null);
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

  private withCacheBust(path: string, version: string | number): string {
    const fileId = path.split('/').pop()?.split('?')[0] ?? '';
    const sep = path.includes('?') ? '&' : '?';
    const v = version === 0 || version === '0' || version === '' ? fileId : String(version);
    return `${path}${sep}v=${encodeURIComponent(v || '1')}`;
  }

  /** Reject JSON/problem bodies that can arrive as blobs on error-ish content. */
  private isRejectedContent(blob: Blob, headerType: string | null): boolean {
    const type = (blob.type || headerType || '').toLowerCase();
    if (!type) {
      return false;
    }
    if (type.startsWith('image/')) {
      return false;
    }
    return type.includes('json') || type.startsWith('text/');
  }

  private revokeObjectUrl(): void {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
  }
}
