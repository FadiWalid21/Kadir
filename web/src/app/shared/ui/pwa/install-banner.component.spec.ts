import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { provideTestTransloco } from '../../../testing/transloco-testing';
import { InstallPromptService } from '../../../core/pwa/install-prompt.service';
import { InstallBannerComponent } from './install-banner.component';

describe('InstallBannerComponent', () => {
  let fixture: ComponentFixture<InstallBannerComponent>;
  const showBanner = signal(false);
  const canInstall = signal(false);
  const promptInstall = vi.fn();
  const openHowToInstall = vi.fn();
  const dismissBanner = vi.fn();

  beforeEach(async () => {
    showBanner.set(false);
    canInstall.set(false);
    promptInstall.mockReset();
    openHowToInstall.mockReset();
    dismissBanner.mockReset();

    await TestBed.configureTestingModule({
      imports: [InstallBannerComponent],
      providers: [
        provideTestTransloco(),
        {
          provide: InstallPromptService,
          useValue: {
            showBanner,
            canInstall,
            promptInstall,
            openHowToInstall,
            dismissBanner,
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(InstallBannerComponent);
  });

  it('renders nothing interactive when showBanner is false', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="install-banner"]')).toBeNull();
  });

  it('shows region and Install CTA when canInstall', () => {
    showBanner.set(true);
    canInstall.set(true);
    fixture.detectChanges();
    const region = fixture.nativeElement.querySelector('[data-testid="install-banner"]');
    expect(region).toBeTruthy();
    expect(region.getAttribute('role')).toBe('region');
    const cta = fixture.nativeElement.querySelector(
      '[data-testid="install-banner-cta"]',
    ) as HTMLElement;
    cta.click();
    expect(promptInstall).toHaveBeenCalled();
  });

  it('shows How to install when banner visible without canInstall', () => {
    showBanner.set(true);
    canInstall.set(false);
    fixture.detectChanges();
    const cta = fixture.nativeElement.querySelector(
      '[data-testid="install-banner-cta"]',
    ) as HTMLElement;
    cta.click();
    expect(openHowToInstall).toHaveBeenCalled();
    expect(promptInstall).not.toHaveBeenCalled();
  });
});
