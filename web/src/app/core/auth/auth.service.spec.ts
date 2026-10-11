import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

const sampleMe = {
  id: 'u1',
  email: 'owner@example.com',
  fullName: 'مالك',
  language: 'ar',
  roles: ['Owner'],
  permissions: ['users.read'],
  tenant: {
    id: 't1',
    name: 'شركة الاختبار',
    slug: 'test-co',
  },
  checklist: { inviteTeamDone: false },
};

async function waitForRequest(http: HttpTestingController, url: string): Promise<TestRequest> {
  let req!: TestRequest;
  await vi.waitFor(() => {
    const matches = http.match(url);
    expect(matches.length).toBe(1);
    req = matches[0]!;
  });
  return req;
}

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'login', children: [] }]),
        AuthService,
      ],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  it('restoreSession stores access token in memory and loads me', async () => {
    const restore = service.restoreSession();

    const refresh = await waitForRequest(http, `${environment.apiBaseUrl}/auth/refresh`);
    expect(refresh.request.withCredentials).toBe(true);
    expect(refresh.request.headers.get('X-Requested-With')).toBe('XMLHttpRequest');
    refresh.flush({
      accessToken: 'access-1',
      expiresIn: 900,
      tokenType: 'Bearer',
    });

    const meReq = await waitForRequest(http, `${environment.apiBaseUrl}/me`);
    meReq.flush(sampleMe);

    await restore;

    expect(service.status()).toBe('authenticated');
    expect(service.accessToken()).toBe('access-1');
    expect(service.me()?.fullName).toBe('مالك');
    expect(sessionStorage.length).toBe(0);
    expect(localStorage.getItem('accessToken')).toBeNull();
    http.verify();
  });

  it('logout clears signals and navigates to login', async () => {
    const loginPromise = service.login('slug', 'a@b.com', 'pass');
    (await waitForRequest(http, `${environment.apiBaseUrl}/auth/login`)).flush({
      accessToken: 'tok',
      expiresIn: 900,
      tokenType: 'Bearer',
    });
    (await waitForRequest(http, `${environment.apiBaseUrl}/me`)).flush(sampleMe);
    await loginPromise;

    expect(service.accessToken()).toBe('tok');

    const nav = service.logout();
    const logoutReq = await waitForRequest(http, `${environment.apiBaseUrl}/auth/logout`);
    expect(logoutReq.request.headers.get('X-Requested-With')).toBe('XMLHttpRequest');
    logoutReq.flush(null, { status: 204, statusText: 'No Content' });
    await nav;

    expect(service.accessToken()).toBeNull();
    expect(service.me()).toBeNull();
    expect(service.status()).toBe('anonymous');
    expect(router.url).toBe('/login');
    http.verify();
  });

  it('restoreSession failure yields anonymous with no token', async () => {
    const restore = service.restoreSession();
    (await waitForRequest(http, `${environment.apiBaseUrl}/auth/refresh`)).flush(
      { code: 'auth.invalid_refresh' },
      { status: 401, statusText: 'Unauthorized' },
    );
    await restore;
    expect(service.status()).toBe('anonymous');
    expect(service.accessToken()).toBeNull();
    http.verify();
  });
});
