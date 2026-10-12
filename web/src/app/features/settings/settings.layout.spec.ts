import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { SettingsLayout } from './settings.layout';

describe('SettingsLayout', () => {
  let permissions: Set<string>;

  beforeEach(async () => {
    permissions = new Set([PermissionCodes.UsersRead, PermissionCodes.TenantSettingsRead]);
    await TestBed.configureTestingModule({
      imports: [SettingsLayout],
      providers: [
        provideRouter([]),
        provideTestTransloco(),
        {
          provide: AuthService,
          useValue: {
            me: signal(null),
            hasPermission: (code: string) => permissions.has(code),
          },
        },
      ],
    }).compileComponents();
  });

  it('hides invitations nav without UsersInvite', () => {
    const fixture = TestBed.createComponent(SettingsLayout);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toMatch(/Invitations|الدعوات/);
  });

  it('shows invitations nav with UsersInvite', () => {
    permissions.add(PermissionCodes.UsersInvite);
    const fixture = TestBed.createComponent(SettingsLayout);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toMatch(/Invitations|الدعوات/);
  });
});
