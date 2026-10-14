import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type IconName =
  | 'home'
  | 'users'
  | 'clock'
  | 'calendar'
  | 'inbox'
  | 'chart'
  | 'settings'
  | 'search'
  | 'bell'
  | 'globe'
  | 'sun'
  | 'moon'
  | 'chevrons'
  | 'more-horizontal'
  | 'user'
  | 'check'
  | 'shield'
  | 'building'
  | 'file'
  | 'lock'
  | 'fingerprint'
  | 'info'
  | 'alert'
  | 'eye'
  | 'eye-off'
  | 'chevron-end'
  | 'chevron-down'
  | 'x'
  | 'circle'
  | 'key'
  | 'link'
  | 'unlink'
  | 'logout'
  | 'download'
  | 'wifi-off'
  | 'refresh'
  | 'share'
  | 'plus-square'
  | 'pause'
  | 'play'
  | 'map-pin';

/** Inline stroke icons (Lucide-like). Logical flip via rtl-flip class when needed. */
@Component({
  selector: 'kadr-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'inline-flex shrink-0',
    '[attr.aria-hidden]': 'true',
  },
  template: `
    <svg
      [attr.viewBox]="'0 0 24 24'"
      fill="none"
      stroke="currentColor"
      stroke-width="1.5"
      stroke-linecap="round"
      stroke-linejoin="round"
      [class]="sizeClass()"
      [class.rtl-flip]="rtlFlip()"
    >
      @switch (name()) {
        @case ('home') {
          <path d="M3 10.5 12 3l9 7.5" />
          <path d="M5 10v10h14V10" />
          <path d="M10 20v-6h4v6" />
        }
        @case ('users') {
          <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
          <circle cx="9" cy="7" r="4" />
          <path d="M22 21v-2a4 4 0 0 0-3-3.87" />
          <path d="M16 3.13a4 4 0 0 1 0 7.75" />
        }
        @case ('clock') {
          <circle cx="12" cy="12" r="9" />
          <path d="M12 7v5l3 2" />
        }
        @case ('calendar') {
          <rect x="3" y="5" width="18" height="16" rx="2" />
          <path d="M3 10h18" />
          <path d="M8 3v4" />
          <path d="M16 3v4" />
        }
        @case ('inbox') {
          <path d="M22 12h-6l-2 3h-4l-2-3H2" />
          <path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z" />
        }
        @case ('chart') {
          <path d="M3 3v18h18" />
          <path d="M7 14v4" />
          <path d="M12 10v8" />
          <path d="M17 6v12" />
        }
        @case ('settings') {
          <circle cx="12" cy="12" r="3" />
          <path
            d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9c.3.6.9 1 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"
          />
        }
        @case ('search') {
          <circle cx="11" cy="11" r="7" />
          <path d="m20 20-3.5-3.5" />
        }
        @case ('bell') {
          <path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9" />
          <path d="M10 21a2 2 0 0 0 4 0" />
        }
        @case ('globe') {
          <circle cx="12" cy="12" r="9" />
          <path d="M3 12h18" />
          <path d="M12 3a14 14 0 0 1 0 18" />
          <path d="M12 3a14 14 0 0 0 0 18" />
        }
        @case ('sun') {
          <circle cx="12" cy="12" r="4" />
          <path d="M12 2v2" />
          <path d="M12 20v2" />
          <path d="m4.9 4.9 1.4 1.4" />
          <path d="m17.7 17.7 1.4 1.4" />
          <path d="M2 12h2" />
          <path d="M20 12h2" />
          <path d="m4.9 19.1 1.4-1.4" />
          <path d="m17.7 6.3 1.4-1.4" />
        }
        @case ('moon') {
          <path d="M21 14.5A8.5 8.5 0 1 1 9.5 3 7 7 0 0 0 21 14.5z" />
        }
        @case ('chevrons') {
          <path d="m15 18-6-6 6-6" />
        }
        @case ('more-horizontal') {
          <circle cx="5" cy="12" r="1.5" fill="currentColor" stroke="none" />
          <circle cx="12" cy="12" r="1.5" fill="currentColor" stroke="none" />
          <circle cx="19" cy="12" r="1.5" fill="currentColor" stroke="none" />
        }
        @case ('user') {
          <circle cx="12" cy="8" r="4" />
          <path d="M4 20a8 8 0 0 1 16 0" />
        }
        @case ('check') {
          <path d="M20 6 9 17l-5-5" />
        }
        @case ('shield') {
          <path d="M12 3 4 6v6c0 5 3.5 8.5 8 9.5 4.5-1 8-4.5 8-9.5V6l-8-3z" />
        }
        @case ('building') {
          <path d="M4 21h16" />
          <path d="M6 21V5a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v16" />
          <path d="M9 8h1" />
          <path d="M14 8h1" />
          <path d="M9 12h1" />
          <path d="M14 12h1" />
          <path d="M9 16h1" />
          <path d="M14 16h1" />
        }
        @case ('file') {
          <path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z" />
          <path d="M14 3v5h5" />
        }
        @case ('lock') {
          <rect x="4" y="10" width="16" height="11" rx="2" />
          <path d="M8 10V7a4 4 0 0 1 8 0v3" />
        }
        @case ('fingerprint') {
          <path d="M12 11a3 3 0 0 0-3 3v5" />
          <path d="M12 7a7 7 0 0 0-7 7v1" />
          <path d="M19 14v1a7 7 0 0 1-2 4.9" />
          <path d="M12 15v4" />
          <path d="M9 18.5V20" />
          <path d="M15 17v3" />
        }
        @case ('info') {
          <circle cx="12" cy="12" r="9" />
          <path d="M12 16v-4" />
          <path d="M12 8h.01" />
        }
        @case ('alert') {
          <path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z" />
          <path d="M12 9v4" />
          <path d="M12 17h.01" />
        }
        @case ('eye') {
          <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z" />
          <circle cx="12" cy="12" r="3" />
        }
        @case ('eye-off') {
          <path d="M9.9 5.2A10 10 0 0 1 12 5c6.5 0 10 7 10 7a17 17 0 0 1-3 3.9" />
          <path d="M6.6 6.6A17 17 0 0 0 2 12s3.5 7 10 7a9.7 9.7 0 0 0 4.4-1" />
          <path d="m3 3 18 18" />
          <path d="M10.6 10.6a3 3 0 0 0 4.2 4.2" />
        }
        @case ('chevron-end') {
          <path d="m9 18 6-6-6-6" />
        }
        @case ('chevron-down') {
          <path d="m6 9 6 6 6-6" />
        }
        @case ('x') {
          <path d="M18 6 6 18" />
          <path d="m6 6 12 12" />
        }
        @case ('circle') {
          <circle cx="12" cy="12" r="9" />
        }
        @case ('key') {
          <circle cx="8" cy="15" r="4" />
          <path d="m10.8 12.2 9.2-9.2" />
          <path d="m17 6 3 3" />
          <path d="m14 9 2 2" />
        }
        @case ('link') {
          <path d="M10 13a5 5 0 0 0 7.1 0l3-3a5 5 0 0 0-7.1-7.1l-1.7 1.7" />
          <path d="M14 11a5 5 0 0 0-7.1 0l-3 3a5 5 0 0 0 7.1 7.1l1.7-1.7" />
        }
        @case ('unlink') {
          <path d="m18.8 13.3 1.3-1.3a5 5 0 0 0-7.1-7.1l-1.3 1.3" />
          <path d="m5.2 10.7-1.3 1.3a5 5 0 0 0 7.1 7.1l1.3-1.3" />
          <path d="m8 2 1 3" />
          <path d="m2 8 3 1" />
          <path d="m16 22-1-3" />
          <path d="m22 16-3-1" />
        }
        @case ('logout') {
          <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
          <path d="m16 17 5-5-5-5" />
          <path d="M21 12H9" />
        }
        @case ('download') {
          <path d="M12 3v12" />
          <path d="m7 10 5 5 5-5" />
          <path d="M5 21h14" />
        }
        @case ('wifi-off') {
          <path d="M12 20h.01" />
          <path d="M8.5 16.4a5 5 0 0 1 7 0" />
          <path d="M5 12.9a10 10 0 0 1 5.2-2.7" />
          <path d="M19 12.9a10 10 0 0 0-2.1-1.5" />
          <path d="M2 8.8A16 16 0 0 1 8.4 5.3" />
          <path d="M22 8.8a16 16 0 0 0-9.2-3.5" />
          <path d="m2 2 20 20" />
        }
        @case ('refresh') {
          <path d="M21 12a9 9 0 1 1-2.6-6.4" />
          <path d="M21 3v6h-6" />
        }
        @case ('share') {
          <path d="M4 12v8a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-8" />
          <path d="m16 6-4-4-4 4" />
          <path d="M12 2v13" />
        }
        @case ('plus-square') {
          <rect width="18" height="18" x="3" y="3" rx="2" />
          <path d="M12 8v8" />
          <path d="M8 12h8" />
        }
        @case ('pause') {
          <rect x="6" y="5" width="4" height="14" rx="1" />
          <rect x="14" y="5" width="4" height="14" rx="1" />
        }
        @case ('play') {
          <path d="M7 4.5v15l12-7.5z" />
        }
        @case ('map-pin') {
          <path d="M12 21s-7-5.3-7-11a7 7 0 1 1 14 0c0 5.7-7 11-7 11z" />
          <circle cx="12" cy="10" r="2.5" />
        }
      }
    </svg>
  `,
})
export class IconComponent {
  readonly name = input.required<IconName>();
  readonly size = input<'sm' | 'md' | 'lg'>('md');
  readonly rtlFlip = input(false);

  protected readonly sizeClass = computed(() => {
    switch (this.size()) {
      case 'sm':
        return 'size-4';
      case 'lg':
        return 'size-6';
      default:
        return 'size-5';
    }
  });
}
