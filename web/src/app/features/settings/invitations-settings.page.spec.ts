import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { InvitationsSettingsPage } from './invitations-settings.page';
import { UsersStore } from './users.store';

describe('InvitationsSettingsPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InvitationsSettingsPage],
      providers: [
        provideRouter([]),
        provideTestTransloco(),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            me: signal(null),
            hasPermission: () => true,
          },
        },
        {
          provide: SubscriptionStore,
          useValue: {
            readOnly: signal(false),
          },
        },
        {
          provide: UsersStore,
          useValue: {
            invitesState: signal('ready'),
            invitations: signal([]),
            roles: signal([]),
            busy: signal(false),
            loadRoles: vi.fn().mockResolvedValue(undefined),
            loadInvitations: vi.fn().mockResolvedValue(undefined),
          },
        },
      ],
    })
      .overrideComponent(InvitationsSettingsPage, {
        set: { providers: [] },
      })
      .compileComponents();
  });

  it('renders invitations heading', async () => {
    const fixture = TestBed.createComponent(InvitationsSettingsPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toMatch(/Invitations|الدعوات/);
  });
});
