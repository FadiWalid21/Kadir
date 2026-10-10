import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { TenantTemplatesApi } from '../../core/api/tenant-templates.api';
import { AuthService } from '../../core/auth/auth.service';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { TemplatesSettingsPage } from './templates-settings.page';

describe('TemplatesSettingsPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TemplatesSettingsPage],
      providers: [
        provideRouter([]),
        provideTestTransloco(),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: TenantTemplatesApi,
          useValue: {
            getOverview: vi.fn().mockResolvedValue({
              industryCode: 'office',
              industryNameAr: 'المكاتب والخدمات',
              industryNameEn: 'Office & Services',
              appliedVersion: 1,
              latestVersion: 1,
              updateAvailable: false,
              missingKeys: [],
            }),
            listSection: vi.fn().mockResolvedValue({
              items: [
                {
                  id: 's1',
                  section: 'shifts',
                  sourceKey: 'shift.morning',
                  sourceVersion: 1,
                  isCustomized: false,
                  isActive: true,
                  nameAr: 'وردية صباحية طويلة للاختبار',
                  nameEn: 'Morning',
                  fields: { startTime: '09:00', endTime: '17:00', graceMinutes: 15 },
                },
              ],
              page: 1,
              pageSize: 50,
              totalCount: 1,
            }),
            updateItem: vi.fn(),
            resetItem: vi.fn().mockResolvedValue({
              id: 's1',
              section: 'shifts',
              sourceKey: 'shift.morning',
              sourceVersion: 1,
              isCustomized: false,
              isActive: true,
              nameAr: 'وردية صباحية طويلة للاختبار',
              nameEn: 'Morning',
              fields: {},
            }),
            setActive: vi.fn(),
            addMissing: vi.fn(),
          },
        },
        {
          provide: AuthService,
          useValue: {
            me: signal({
              id: 'u1',
              fullName: 'مالك',
              email: 'o@example.com',
              language: 'ar',
              roles: ['Owner'],
              permissions: ['tenant.templates.manage'],
              tenant: { id: 't1', name: 'شركة', slug: 't', logoUrl: null },
            }),
            hasPermission: () => true,
          },
        },
      ],
    }).compileComponents();
  });

  it('renders templates tab content in Arabic', async () => {
    const fixture = TestBed.createComponent(TemplatesSettingsPage);
    const page = fixture.componentInstance;
    fixture.detectChanges();
    await page.reload();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('قوالب القطاع');
    expect(text).toContain('المكاتب والخدمات');
    expect(text).toContain('وردية صباحية طويلة للاختبار');
    expect(fixture.nativeElement.querySelector('kadr-tabs')).toBeTruthy();
  });

  it('opens drawer for edit and can confirm reset flow', async () => {
    const fixture = TestBed.createComponent(TemplatesSettingsPage);
    const page = fixture.componentInstance;
    fixture.detectChanges();
    await page.reload();
    fixture.detectChanges();

    const item = page['store'].items()[0];
    expect(item).toBeTruthy();
    page.openItem(item);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('kadr-drawer')).toBeTruthy();

    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    // mark customized so reset button shows — mutate via store
    page['store'].items.update((list) =>
      list.map((x) => (x.id === item.id ? { ...x, isCustomized: true } : x)),
    );
    fixture.detectChanges();
    await page.onReset({ ...item, isCustomized: true });
    expect(confirmSpy).toHaveBeenCalled();
    confirmSpy.mockRestore();
  });
});
