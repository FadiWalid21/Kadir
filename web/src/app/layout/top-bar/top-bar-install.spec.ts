import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { AuthService } from '../../core/auth/auth.service';
import { InstallPromptService } from '../../core/pwa/install-prompt.service';
import { NotificationsStore } from '../../core/notifications/notifications.store';
import { ThemeService } from '../../core/theme/theme.service';
import { TopBarComponent } from './top-bar.component';

describe('TopBarComponent install menu', () => {
  const showMenuItem = signal(false);

  beforeEach(async () => {
    showMenuItem.set(false);
    await TestBed.configureTestingModule({
      imports: [TopBarComponent],
      providers: [
        provideRouter([]),
        provideTestTransloco(),
        ThemeService,
        {
          provide: AuthService,
          useValue: {
            me: signal({
              fullName: 'Test',
              email: 't@example.com',
              roles: ['Owner'],
              photoUrl: null,
              tenant: { name: 'Co', logoUrl: null },
            }),
            logout: vi.fn(),
          },
        },
        {
          provide: InstallPromptService,
          useValue: {
            showMenuItem,
            promptInstall: vi.fn(),
          },
        },
        {
          provide: NotificationsStore,
          useValue: {
            unreadCount: signal(0),
            preview: signal([]),
            previewState: signal('ready'),
            refreshUnread: vi.fn(),
            loadPreview: vi.fn(),
            markRead: vi.fn(),
            markAllRead: vi.fn(),
          },
        },
      ],
    }).compileComponents();
  });

  function openUserMenu(fixture: ComponentFixture<TopBarComponent>): void {
    const triggers = Array.from(
      fixture.nativeElement.querySelectorAll('button[aria-haspopup="menu"]'),
    ) as HTMLButtonElement[];
    // User menu is the last dropdown (after notifications).
    triggers[triggers.length - 1]?.click();
    fixture.detectChanges();
  }

  it('hides Install menu item when showMenuItem is false', () => {
    const fixture = TestBed.createComponent(TopBarComponent);
    fixture.detectChanges();
    openUserMenu(fixture);
    expect(fixture.nativeElement.querySelector('[data-testid="menu-install"]')).toBeNull();
    expect(
      fixture.nativeElement.querySelector('[data-testid="menu-install-placeholder"]'),
    ).toBeNull();
  });

  it('shows Install menu item when showMenuItem is true', () => {
    showMenuItem.set(true);
    const fixture = TestBed.createComponent(TopBarComponent);
    fixture.detectChanges();
    openUserMenu(fixture);
    expect(fixture.nativeElement.querySelector('[data-testid="menu-install"]')).toBeTruthy();
  });
});
