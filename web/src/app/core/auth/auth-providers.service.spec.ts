import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthApi } from '../api/auth.api';
import { AuthProvidersService } from './auth-providers.service';

describe('AuthProvidersService', () => {
  let getProviders: ReturnType<typeof vi.fn>;

  function create(): AuthProvidersService {
    TestBed.configureTestingModule({
      providers: [AuthProvidersService, { provide: AuthApi, useValue: { getProviders } }],
    });
    return TestBed.inject(AuthProvidersService);
  }

  beforeEach(() => {
    getProviders = vi.fn();
  });

  it('enables Google only when the API reports enabled with a client id', async () => {
    getProviders.mockResolvedValue({ google: { enabled: true, clientId: 'client-1' } });
    const service = create();
    expect(service.googleEnabled()).toBe(false);

    await service.load();
    expect(service.status()).toBe('ready');
    expect(service.googleEnabled()).toBe(true);
    expect(service.googleClientId()).toBe('client-1');
  });

  it('keeps Google hidden when disabled or the client id is empty', async () => {
    getProviders.mockResolvedValue({ google: { enabled: true, clientId: '' } });
    const service = create();
    await service.load();
    expect(service.googleEnabled()).toBe(false);
  });

  it('keeps Google hidden when the API says disabled', async () => {
    getProviders.mockResolvedValue({ google: { enabled: false, clientId: null } });
    const service = create();
    await service.load();
    expect(service.status()).toBe('ready');
    expect(service.googleEnabled()).toBe(false);
  });

  it('requests providers once and shares in-flight calls', async () => {
    getProviders.mockResolvedValue({ google: { enabled: true, clientId: 'c' } });
    const service = create();
    await Promise.all([service.load(), service.load()]);
    await service.load();
    expect(getProviders).toHaveBeenCalledTimes(1);
  });

  it('hides Google on failure and allows a retry', async () => {
    getProviders.mockRejectedValueOnce(new Error('network'));
    const service = create();
    await service.load();
    expect(service.status()).toBe('error');
    expect(service.googleEnabled()).toBe(false);

    getProviders.mockResolvedValueOnce({ google: { enabled: true, clientId: 'c' } });
    await service.load();
    expect(service.googleEnabled()).toBe(true);
  });
});
