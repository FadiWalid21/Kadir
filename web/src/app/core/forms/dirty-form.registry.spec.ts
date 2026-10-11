import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { DirtyFormRegistry } from './dirty-form.registry';

describe('DirtyFormRegistry', () => {
  it('tracks dirty ids', () => {
    const registry = TestBed.inject(DirtyFormRegistry);
    expect(registry.hasDirty()).toBe(false);
    registry.register('invite');
    expect(registry.hasDirty()).toBe(true);
    registry.clear('invite');
    expect(registry.hasDirty()).toBe(false);
  });
});
