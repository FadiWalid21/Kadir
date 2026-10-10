import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../core/auth/auth.service';
import { provideTestTransloco } from '../testing/transloco-testing';
import { HomePage } from './home.page';

const me = {
  id: 'u1',
  fullName: 'مالك الاختبار',
  email: 'owner@example.com',
  language: 'ar',
  roles: ['Owner'],
  permissions: [] as string[],
  tenant: { id: 't1', name: 'شركة الاختبار', slug: 'test-co' },
};

describe('HomePage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HomePage],
      providers: [
        provideRouter([]),
        provideTestTransloco(),
        Title,
        {
          provide: AuthService,
          useValue: {
            me: signal(me),
            status: signal('authenticated'),
            restoreSession: vi.fn().mockResolvedValue(undefined),
          },
        },
      ],
    }).compileComponents();
  });

  it('renders loading, empty checklist, and error states in Arabic', async () => {
    const fixture = TestBed.createComponent(HomePage);
    const page = fixture.componentInstance;

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    let text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('جاهزية مكان العمل');
    expect(text).toContain('إضافة شعار الشركة');
    expect(text).toContain('غير متاح بعد');
    expect(text).toContain('0 / 4');
    expect(text).toContain('الموافقات');

    page.setStateForTest('loading');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('kadr-skeleton')).toBeTruthy();

    page.setStateForTest('error');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('تعذر تحميل الصفحة');
  });
});
