import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, beforeEach } from 'vitest';
import { AuthService } from './auth.service';
import { guestGuard, landingGuard } from './auth.guard';

describe('landing and guest home redirects', () => {
  const status = signal<'anonymous' | 'authenticated' | 'loading'>('anonymous');

  beforeEach(() => {
    status.set('anonymous');
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            status,
            restoreSession: async () => undefined,
          },
        },
      ],
    });
  });

  it('landingGuard allows guests on /', async () => {
    const result = await TestBed.runInInjectionContext(() => landingGuard({} as never, {} as never));
    expect(result).toBe(true);
  });

  it('landingGuard redirects authenticated users to /home', async () => {
    status.set('authenticated');
    const result = await TestBed.runInInjectionContext(() => landingGuard({} as never, {} as never));
    expect(String(result)).toContain('/home');
  });

  it('guestGuard redirects authenticated users to /home', async () => {
    status.set('authenticated');
    const result = await TestBed.runInInjectionContext(() => guestGuard({} as never, {} as never));
    expect(String(result)).toContain('/home');
  });
});
