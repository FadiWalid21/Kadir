import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { authGuard, guestGuard, permissionGuard } from './auth.guard';
import { AuthService } from './auth.service';
import { PermissionCodes } from './permission-codes';

describe('auth guards', () => {
  let auth: AuthService;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'login', children: [] },
          { path: 'register', children: [] },
          { path: 'forbidden', children: [] },
          { path: '', children: [] },
        ]),
        {
          provide: AuthService,
          useValue: {
            restoreSession: vi.fn().mockResolvedValue(undefined),
            status: vi.fn().mockReturnValue('anonymous'),
            hasPermission: vi.fn().mockReturnValue(false),
          },
        },
      ],
    });
    auth = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
  });

  it('authGuard redirects unauthenticated users to login', async () => {
    const result = await TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));
    expect(result instanceof UrlTree).toBe(true);
    expect(router.serializeUrl(result as UrlTree)).toBe('/login');
    expect(auth.restoreSession).toHaveBeenCalled();
  });

  it('authGuard allows authenticated users', async () => {
    vi.mocked(auth.status).mockReturnValue('authenticated');
    const result = await TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));
    expect(result).toBe(true);
  });

  it('guestGuard redirects authenticated users home', async () => {
    vi.mocked(auth.status).mockReturnValue('authenticated');
    const result = await TestBed.runInInjectionContext(() => guestGuard({} as never, {} as never));
    expect(result instanceof UrlTree).toBe(true);
    expect(router.serializeUrl(result as UrlTree)).toBe('/home');
  });

  it('guestGuard allows anonymous users on register', async () => {
    vi.mocked(auth.status).mockReturnValue('anonymous');
    const result = await TestBed.runInInjectionContext(() => guestGuard({} as never, {} as never));
    expect(result).toBe(true);
  });

  it('permissionGuard blocks missing permission', async () => {
    vi.mocked(auth.status).mockReturnValue('authenticated');
    vi.mocked(auth.hasPermission).mockReturnValue(false);
    const guard = permissionGuard(PermissionCodes.UsersInvite);
    const result = await TestBed.runInInjectionContext(() => guard({} as never, {} as never));
    expect(result instanceof UrlTree).toBe(true);
    expect(router.serializeUrl(result as UrlTree)).toBe('/forbidden');
  });

  it('permissionGuard allows when permission present', async () => {
    vi.mocked(auth.status).mockReturnValue('authenticated');
    vi.mocked(auth.hasPermission).mockReturnValue(true);
    const guard = permissionGuard(PermissionCodes.UsersInvite);
    const result = await TestBed.runInInjectionContext(() => guard({} as never, {} as never));
    expect(result).toBe(true);
  });
});
