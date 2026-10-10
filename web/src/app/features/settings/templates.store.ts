import { computed, inject, Injectable, signal } from '@angular/core';
import {
  TemplateItem,
  TemplateSection,
  TenantTemplatesApi,
  TenantTemplatesOverview,
} from '../../core/api/tenant-templates.api';

export type TemplatesLoadState = 'idle' | 'loading' | 'ready' | 'error';

@Injectable()
export class TemplatesStore {
  private readonly api = inject(TenantTemplatesApi);

  readonly overview = signal<TenantTemplatesOverview | null>(null);
  readonly section = signal<TemplateSection>('shifts');
  readonly items = signal<TemplateItem[]>([]);
  readonly totalCount = signal(0);
  readonly query = signal('');
  readonly overviewState = signal<TemplatesLoadState>('idle');
  readonly listState = signal<TemplatesLoadState>('idle');
  readonly drawerItem = signal<TemplateItem | null>(null);
  readonly busy = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly updateAvailable = computed(() => this.overview()?.updateAvailable === true);

  async loadOverview(): Promise<void> {
    this.overviewState.set('loading');
    this.errorMessage.set(null);
    try {
      this.overview.set(await this.api.getOverview());
      this.overviewState.set('ready');
    } catch {
      this.overviewState.set('error');
      this.errorMessage.set('load');
    }
  }

  async loadSection(section: TemplateSection = this.section()): Promise<void> {
    this.section.set(section);
    this.listState.set('loading');
    try {
      const page = await this.api.listSection(section, {
        page: 1,
        pageSize: 100,
        q: this.query() || undefined,
      });
      this.items.set(page.items);
      this.totalCount.set(page.totalCount);
      this.listState.set('ready');
    } catch {
      this.listState.set('error');
    }
  }

  openDrawer(item: TemplateItem): void {
    this.drawerItem.set(item);
  }

  closeDrawer(): void {
    this.drawerItem.set(null);
  }

  async saveDrawer(body: unknown): Promise<boolean> {
    const item = this.drawerItem();
    if (!item) {
      return false;
    }
    this.busy.set(true);
    try {
      const updated = await this.api.updateItem(this.section(), item.id, body);
      this.items.update((list) => list.map((x) => (x.id === updated.id ? updated : x)));
      this.drawerItem.set(null);
      return true;
    } catch {
      return false;
    } finally {
      this.busy.set(false);
    }
  }

  async resetItem(item: TemplateItem): Promise<boolean> {
    this.busy.set(true);
    try {
      const updated = await this.api.resetItem(this.section(), item.id);
      this.items.update((list) => list.map((x) => (x.id === updated.id ? updated : x)));
      if (this.drawerItem()?.id === updated.id) {
        this.drawerItem.set(updated);
      }
      return true;
    } catch {
      return false;
    } finally {
      this.busy.set(false);
    }
  }

  async toggleActive(item: TemplateItem, isActive: boolean): Promise<boolean> {
    this.busy.set(true);
    try {
      const updated = await this.api.setActive(this.section(), item.id, isActive);
      this.items.update((list) => list.map((x) => (x.id === updated.id ? updated : x)));
      return true;
    } catch {
      return false;
    } finally {
      this.busy.set(false);
    }
  }

  async addMissing(): Promise<number> {
    this.busy.set(true);
    try {
      const result = await this.api.addMissing();
      await this.loadOverview();
      await this.loadSection();
      return result.addedCount;
    } catch {
      return -1;
    } finally {
      this.busy.set(false);
    }
  }
}
