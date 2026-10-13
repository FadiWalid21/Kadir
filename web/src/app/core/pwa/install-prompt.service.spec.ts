/**
 * Manual install checks (prod build / HTTPS only — SW is off under ng serve):
 *
 * Chrome desktop: `npm run pwa:serve`, open http://localhost:4173/login,
 * DevTools → Application → Manifest + Service Workers, confirm beforeinstallprompt
 * is captured (banner appears), Install, dismiss (banner gone, reload stays gone),
 * sign in → avatar menu still shows Install when canInstall.
 *
 * Android Chrome: open the deployed HTTPS URL (or tunnel to pwa:serve), confirm
 * slim top banner before login, Install uses the native sheet, standalone hides banner.
 *
 * iOS Safari: not standalone → banner shows "How to install" → Share / Add to Home
 * Screen dialog (text steps). After Add to Home Screen / standalone, banner and menu hide.
 */
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { InstallPromptService } from './install-prompt.service';

function stubMatchMedia(standalone = false): void {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    configurable: true,
    value: (query: string) =>
      ({
        matches: standalone && query.includes('display-mode: standalone'),
        media: query,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        dispatchEvent: vi.fn(),
      }) as MediaQueryList,
  });
}

function fireBeforeInstallPrompt(): void {
  const event = new Event('beforeinstallprompt', { cancelable: true });
  Object.defineProperty(event, 'prompt', {
    value: vi.fn().mockResolvedValue(undefined),
  });
  Object.defineProperty(event, 'userChoice', {
    value: Promise.resolve({ outcome: 'accepted' as const }),
  });
  window.dispatchEvent(event);
}

describe('InstallPromptService', () => {
  beforeEach(() => {
    localStorage.clear();
    stubMatchMedia(false);
    TestBed.configureTestingModule({ providers: [InstallPromptService] });
  });

  afterEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('captures beforeinstallprompt and exposes canInstall', () => {
    const service = TestBed.inject(InstallPromptService);
    fireBeforeInstallPrompt();
    expect(service.canInstall()).toBe(true);
    expect(service.showBanner()).toBe(true);
    expect(service.showMenuItem()).toBe(true);
  });

  it('shows menu item when not installed even without BIP or iOS', () => {
    const service = TestBed.inject(InstallPromptService);
    expect(service.canInstall()).toBe(false);
    expect(service.isIos()).toBe(false);
    expect(service.isInstalled()).toBe(false);
    expect(service.showMenuItem()).toBe(true);
    expect(service.showBanner()).toBe(false);
  });

  it('hides install UI when standalone (isInstalled)', () => {
    stubMatchMedia(true);
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [InstallPromptService] });
    const service = TestBed.inject(InstallPromptService);
    expect(service.isInstalled()).toBe(true);
    expect(service.showBanner()).toBe(false);
    expect(service.showMenuItem()).toBe(false);
  });

  it('dismiss hides banner but keeps menu when still installable', () => {
    const service = TestBed.inject(InstallPromptService);
    fireBeforeInstallPrompt();
    expect(service.showBanner()).toBe(true);

    service.dismissBanner();
    expect(service.showBanner()).toBe(false);
    expect(service.showMenuItem()).toBe(true);
    expect(localStorage.getItem('kadr.pwa.installBannerDismissed')).toBe('1');

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [InstallPromptService] });
    const again = TestBed.inject(InstallPromptService);
    fireBeforeInstallPrompt();
    expect(again.showBanner()).toBe(false);
    expect(again.showMenuItem()).toBe(true);
  });

  it('dismiss keeps menu visible even without BIP', () => {
    const service = TestBed.inject(InstallPromptService);
    service.dismissBanner();
    expect(service.showBanner()).toBe(false);
    expect(service.showMenuItem()).toBe(true);
  });

  it('promptInstall without BIP opens how-to dialog', async () => {
    const service = TestBed.inject(InstallPromptService);
    const accepted = await service.promptInstall();
    expect(accepted).toBe(false);
    expect(service.showHowTo()).toBe(true);
  });

  it('appinstalled clears the install prompt', () => {
    const service = TestBed.inject(InstallPromptService);
    fireBeforeInstallPrompt();
    expect(service.canInstall()).toBe(true);
    window.dispatchEvent(new Event('appinstalled'));
    expect(service.canInstall()).toBe(false);
    expect(service.isInstalled()).toBe(true);
    expect(service.showBanner()).toBe(false);
    expect(service.showMenuItem()).toBe(false);
  });

  it('iOS path shows banner and menu without BIP when not dismissed', () => {
    Object.defineProperty(navigator, 'userAgent', {
      configurable: true,
      value:
        'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1',
    });
    Object.defineProperty(navigator, 'platform', {
      configurable: true,
      value: 'iPhone',
    });
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [InstallPromptService] });
    const service = TestBed.inject(InstallPromptService);
    expect(service.isIos()).toBe(true);
    expect(service.canInstall()).toBe(false);
    expect(service.showBanner()).toBe(true);
    expect(service.showMenuItem()).toBe(true);
    service.dismissBanner();
    expect(service.showBanner()).toBe(false);
    expect(service.showMenuItem()).toBe(true);
  });
});
