import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { environment } from '../../../environments/environment';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { SubscriptionApi } from '../api/subscription.api';
import { subscriptionErrorInterceptor } from './subscription-error.interceptor';
import { SubscriptionStore } from './subscription.store';

describe('subscriptionErrorInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let toast: ToastService;
  let store: SubscriptionStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([subscriptionErrorInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTestTransloco(),
        ToastService,
        SubscriptionStore,
        {
          provide: SubscriptionApi,
          useValue: {
            getSubscription: vi.fn().mockResolvedValue({
              usage: { cap: 25, activeEmployees: 26 },
              effectiveState: 'active',
              trialEndsAt: null,
              daysLeft: null,
              readOnly: false,
              features: {},
            }),
          },
        },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    toast = TestBed.inject(ToastService);
    store = TestBed.inject(SubscriptionStore);
  });

  it('maps subscription.expired to toast with link', async () => {
    const pending = firstValueFrom(http.post(`${environment.apiBaseUrl}/roles`, {})).catch(
      (e) => e,
    );
    const req = httpMock.expectOne(`${environment.apiBaseUrl}/roles`);
    req.flush(
      { code: 'subscription.expired', detail: 'expired' },
      { status: 403, statusText: 'Forbidden' },
    );
    await pending;
    expect(toast.messages().some((m) => m.link?.url === '/settings/subscription')).toBe(true);
  });

  it('maps plan.feature_not_available to toast', async () => {
    const pending = firstValueFrom(http.post(`${environment.apiBaseUrl}/x`, {})).catch((e) => e);
    const req = httpMock.expectOne(`${environment.apiBaseUrl}/x`);
    req.flush(
      { code: 'plan.feature_not_available', detail: 'no' },
      { status: 403, statusText: 'Forbidden' },
    );
    await pending;
    expect(toast.messages().length).toBeGreaterThan(0);
  });

  it('maps plan.employee_cap_exceeded to seat dialog', async () => {
    const pending = firstValueFrom(http.post(`${environment.apiBaseUrl}/y`, {})).catch((e) => e);
    const req = httpMock.expectOne(`${environment.apiBaseUrl}/y`);
    req.flush(
      { code: 'plan.employee_cap_exceeded', detail: 'cap' },
      { status: 403, statusText: 'Forbidden' },
    );
    await pending;
    await vi.waitFor(() => {
      expect(store.seatDialogOpen()).toBe(true);
      expect(store.seatCap()).toBe(25);
    });
  });
});
