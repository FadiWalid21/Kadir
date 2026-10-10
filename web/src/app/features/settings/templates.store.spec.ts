import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { TenantTemplatesApi } from '../../core/api/tenant-templates.api';
import { TemplatesStore } from './templates.store';

describe('TemplatesStore', () => {
  let api: {
    getOverview: ReturnType<typeof vi.fn>;
    listSection: ReturnType<typeof vi.fn>;
    updateItem: ReturnType<typeof vi.fn>;
    resetItem: ReturnType<typeof vi.fn>;
    setActive: ReturnType<typeof vi.fn>;
    addMissing: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    api = {
      getOverview: vi.fn().mockResolvedValue({
        industryCode: 'office',
        industryNameAr: 'مكاتب',
        industryNameEn: 'Office',
        appliedVersion: 1,
        latestVersion: 1,
        updateAvailable: false,
        missingKeys: [],
      }),
      listSection: vi.fn().mockResolvedValue({
        items: [
          {
            id: '1',
            section: 'shifts',
            sourceKey: 'shift.morning',
            sourceVersion: 1,
            isCustomized: false,
            isActive: true,
            nameAr: 'صباحية',
            nameEn: 'Morning',
            fields: {},
          },
        ],
        page: 1,
        pageSize: 50,
        totalCount: 1,
      }),
      updateItem: vi.fn(),
      resetItem: vi.fn(),
      setActive: vi.fn(),
      addMissing: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [TemplatesStore, { provide: TenantTemplatesApi, useValue: api }],
    });
  });

  it('loads overview and section', async () => {
    const store = TestBed.inject(TemplatesStore);
    await store.loadOverview();
    await store.loadSection('shifts');
    expect(store.overviewState()).toBe('ready');
    expect(store.listState()).toBe('ready');
    expect(store.items()).toHaveLength(1);
    expect(api.getOverview).toHaveBeenCalled();
    expect(api.listSection).toHaveBeenCalledWith('shifts', expect.any(Object));
  });
});
