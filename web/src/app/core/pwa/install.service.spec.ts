import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { InstallService } from './install.service';

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

describe('InstallService', () => {
  beforeEach(() => {
    localStorage.clear();
    stubMatchMedia(false);
    TestBed.configureTestingModule({ providers: [InstallService] });
  });

  afterEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('captures beforeinstallprompt and exposes canInstall', () => {
    const service = TestBed.inject(InstallService);
    fireBeforeInstallPrompt();
    expect(service.canInstall()).toBe(true);
  });

  it('hides install UI when standalone', () => {
    stubMatchMedia(true);
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [InstallService] });
    const service = TestBed.inject(InstallService);
    expect(service.isStandalone()).toBe(true);
    expect(service.showHomeCard()).toBe(false);
    expect(service.showMenuItem()).toBe(false);
  });

  it('hides home card for 14 days after dismissal and shows again after', () => {
    const service = TestBed.inject(InstallService);
    fireBeforeInstallPrompt();
    expect(service.showHomeCard()).toBe(true);

    service.dismissHomeCard();
    expect(service.showHomeCard()).toBe(false);
    expect(localStorage.getItem('kadr.pwa.installDismissedAt')).toBeTruthy();

    localStorage.setItem('kadr.pwa.installDismissedAt', String(Date.now() - 15 * 24 * 60 * 60 * 1000));
    stubMatchMedia(false);
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [InstallService] });
    const again = TestBed.inject(InstallService);
    fireBeforeInstallPrompt();
    expect(again.showHomeCard()).toBe(true);
  });

  it('appinstalled clears the install prompt', () => {
    const service = TestBed.inject(InstallService);
    fireBeforeInstallPrompt();
    expect(service.canInstall()).toBe(true);
    window.dispatchEvent(new Event('appinstalled'));
    expect(service.canInstall()).toBe(false);
    expect(service.showHomeCard()).toBe(false);
  });
});
