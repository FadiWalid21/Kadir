import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { offlineInterceptor } from './offline.interceptor';
import { OfflineService } from './offline.service';

describe('OfflineService', () => {
  let offline: OfflineService;
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let toast: ToastService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([offlineInterceptor])),
        provideHttpClientTesting(),
        provideTestTransloco(),
        OfflineService,
        ToastService,
      ],
    });
    offline = TestBed.inject(OfflineService);
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    toast = TestBed.inject(ToastService);
  });

  it('status 0 sets offline and a success clears it with a restored toast', async () => {
    offline.noteFailedRequest();
    expect(offline.offline()).toBe(true);

    const show = vi.spyOn(toast, 'show');
    const call = firstValueFrom(http.get('/api/v1/health'));
    httpMock.expectOne('/api/v1/health').flush({ status: 'ok' });
    await call;
    expect(offline.offline()).toBe(false);
    expect(show).toHaveBeenCalled();

    const fail = firstValueFrom(http.get('/api/v1/me')).catch(() => 'err');
    httpMock.expectOne('/api/v1/me').error(new ProgressEvent('error'), { status: 0 });
    await fail;
    expect(offline.offline()).toBe(true);
  });

  it('online/offline window events update the signal', () => {
    window.dispatchEvent(new Event('offline'));
    expect(offline.offline()).toBe(true);
    window.dispatchEvent(new Event('online'));
    expect(offline.offline()).toBe(false);
  });
});
