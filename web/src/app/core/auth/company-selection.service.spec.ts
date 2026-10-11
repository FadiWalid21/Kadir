import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CompanySelectionService } from './company-selection.service';

describe('CompanySelectionService', () => {
  afterEach(() => vi.restoreAllMocks());

  const state = {
    selectionToken: 'sel-token-123',
    email: 'a@example.com',
    companies: [{ tenantId: 't1', name: 'شركة', roleName: 'مدير', lastLoginAt: null }],
  };

  it('starts empty, stores in memory and clears', () => {
    const service = TestBed.inject(CompanySelectionService);
    expect(service.state()).toBeNull();
    service.set(state);
    expect(service.state()).toEqual(state);
    service.clear();
    expect(service.state()).toBeNull();
  });

  it('never writes to localStorage or sessionStorage', () => {
    const local = vi.spyOn(Storage.prototype, 'setItem');
    const service = TestBed.inject(CompanySelectionService);
    service.set(state);
    expect(local).not.toHaveBeenCalled();
    expect(JSON.stringify({ ...localStorage, ...sessionStorage })).not.toContain('sel-token-123');
  });

  it('is lost with a new injector (reload)', () => {
    TestBed.inject(CompanySelectionService).set(state);
    TestBed.resetTestingModule();
    expect(TestBed.inject(CompanySelectionService).state()).toBeNull();
  });
});
