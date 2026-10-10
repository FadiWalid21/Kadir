import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { environment } from '../../../environments/environment';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

interface AuthInternals {
  accessTokenSignal: { set: (v: string | null) => void };
  statusSignal: { set: (v: string) => void };
}

function seedToken(auth: AuthService, token: string): void {
  const internals = auth as unknown as AuthInternals;
  internals.accessTokenSignal.set(token);
  internals.statusSignal.set('authenticated');
}

async function waitForRequest(http: HttpTestingController, url: string): Promise<TestRequest> {
  let req!: TestRequest;
  await vi.waitFor(() => {
    const matches = http.match(url);
    expect(matches.length).toBe(1);
    req = matches[0]!;
  });
  return req;
}

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([{ path: 'login', children: [] }]),
        AuthService,
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  it('parallel 401s share a single refresh', async () => {
    seedToken(auth, 'old-token');

    const results = Promise.all([
      firstValueFrom(http.get(`${environment.apiBaseUrl}/secure-a`)),
      firstValueFrom(http.get(`${environment.apiBaseUrl}/secure-b`)),
    ]);

    httpMock
      .expectOne(`${environment.apiBaseUrl}/secure-a`)
      .flush({ code: 'unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    httpMock
      .expectOne(`${environment.apiBaseUrl}/secure-b`)
      .flush({ code: 'unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    const refresh = await waitForRequest(httpMock, `${environment.apiBaseUrl}/auth/refresh`);
    refresh.flush({
      accessToken: 'new-token',
      expiresIn: 900,
      tokenType: 'Bearer',
    });

    const retryA = await waitForRequest(httpMock, `${environment.apiBaseUrl}/secure-a`);
    const retryB = await waitForRequest(httpMock, `${environment.apiBaseUrl}/secure-b`);
    expect(retryA.request.headers.get('Authorization')).toBe('Bearer new-token');
    expect(retryB.request.headers.get('Authorization')).toBe('Bearer new-token');
    retryA.flush({ ok: true });
    retryB.flush({ ok: true });

    await results;
    expect(auth.accessToken()).toBe('new-token');
    httpMock.verify();
  });

  it('failed refresh triggers logout', async () => {
    seedToken(auth, 'old-token');
    const logoutSpy = vi.spyOn(auth, 'logout').mockResolvedValue(undefined);

    const call = firstValueFrom(http.get(`${environment.apiBaseUrl}/secure`)).catch(
      () => 'error',
    );

    httpMock
      .expectOne(`${environment.apiBaseUrl}/secure`)
      .flush({ code: 'unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    (await waitForRequest(httpMock, `${environment.apiBaseUrl}/auth/refresh`)).flush(
      { code: 'auth.invalid_refresh' },
      { status: 401, statusText: 'Unauthorized' },
    );

    await call;
    expect(logoutSpy).toHaveBeenCalled();
    httpMock.verify();
  });
});
