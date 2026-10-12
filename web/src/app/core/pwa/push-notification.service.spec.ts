/**
 * Manual push matrix (pwa:serve / HTTPS deploy — SW off under ng serve):
 * Chrome desktop closed; Android Chrome closed; iOS 16.4+ installed PWA closed;
 * permission denied; unsupported; logout then login as another user.
 */
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PushSubscriptionsApi } from '../api/push-subscriptions.api';
import { InstallPromptService } from './install-prompt.service';
import { PushNotificationService } from './push-notification.service';

describe('PushNotificationService', () => {
  let service: PushNotificationService;
  let api: {
    vapidPublicKey: ReturnType<typeof vi.fn>;
    upsert: ReturnType<typeof vi.fn>;
    delete: ReturnType<typeof vi.fn>;
    list: ReturnType<typeof vi.fn>;
    sendTest: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    api = {
      vapidPublicKey: vi.fn(),
      upsert: vi.fn(),
      delete: vi.fn().mockResolvedValue(undefined),
      list: vi.fn(),
      sendTest: vi.fn(),
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        PushNotificationService,
        { provide: PushSubscriptionsApi, useValue: api },
        InstallPromptService,
      ],
    });
    service = TestBed.inject(PushNotificationService);
  });

  it('does not request permission or subscribe on construction', () => {
    expect(api.vapidPublicKey).not.toHaveBeenCalled();
    expect(api.upsert).not.toHaveBeenCalled();
  });

  it('prepareLogout deletes stored device id', async () => {
    // Access private signal for logout cleanup coverage.
    (service as unknown as { deviceId: { set: (v: string) => void } }).deviceId.set('dev-1');
    await service.prepareLogout();
    expect(api.delete).toHaveBeenCalledWith('dev-1');
    expect(service.state()).not.toBe('subscribed');
  });
});
