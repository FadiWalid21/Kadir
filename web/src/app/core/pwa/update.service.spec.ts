import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { SwUpdate } from '@angular/service-worker';
import { Subject } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DirtyFormRegistry } from '../forms/dirty-form.registry';
import { UpdateService } from './update.service';

describe('UpdateService', () => {
  let versionUpdates: Subject<{ type: string }>;
  let activateUpdate: ReturnType<typeof vi.fn>;
  let checkForUpdate: ReturnType<typeof vi.fn>;
  let reload: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    versionUpdates = new Subject();
    activateUpdate = vi.fn().mockResolvedValue(true);
    checkForUpdate = vi.fn().mockResolvedValue(true);
    reload = vi.fn();

    TestBed.configureTestingModule({
      providers: [
        UpdateService,
        DirtyFormRegistry,
        {
          provide: SwUpdate,
          useValue: {
            isEnabled: true,
            versionUpdates: versionUpdates.asObservable(),
            activateUpdate,
            checkForUpdate,
          },
        },
        {
          provide: DOCUMENT,
          useValue: {
            ...document,
            visibilityState: 'visible',
            addEventListener: document.addEventListener.bind(document),
            defaultView: { location: { reload } },
          },
        },
      ],
    });
  });

  it('shows a ready notice and does not reload until applyUpdate', async () => {
    const service = TestBed.inject(UpdateService);
    expect(service.state()).toBe('hidden');
    versionUpdates.next({ type: 'VERSION_READY' });
    expect(service.state()).toBe('ready');
    expect(reload).not.toHaveBeenCalled();

    await service.applyUpdate();
    expect(activateUpdate).toHaveBeenCalled();
    expect(reload).toHaveBeenCalledOnce();
  });

  it('exposes unsaved changes from the dirty registry', () => {
    const service = TestBed.inject(UpdateService);
    const dirty = TestBed.inject(DirtyFormRegistry);
    expect(service.hasUnsavedChanges()).toBe(false);
    dirty.register('form-1');
    expect(service.hasUnsavedChanges()).toBe(true);
  });
});
