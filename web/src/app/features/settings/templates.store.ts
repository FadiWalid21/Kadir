import { computed, inject, Injectable, signal } from '@angular/core';
import { problemDetailsFromError } from '../../core/api/auth.api';
import {
  TemplateItem,
  TemplateSection,
  TenantTemplatesApi,
  TenantTemplatesOverview,
} from '../../core/api/tenant-templates.api';

export type TemplatesLoadState = 'idle' | 'loading' | 'ready' | 'error';

export type TemplateActionError =
  | { kind: 'in_use' }
  | { kind: 'duplicate' }
  | { kind: 'generic' };

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
  readonly drawerMode = signal<'create' | 'edit'>('edit');
  readonly busy = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly lastError = signal<TemplateActionError | null>(null);

  readonly updateAvailable = computed(() => this.overview()?.updateAvailable === true);
  readonly isCreateMode = computed(() => this.drawerMode() === 'create');

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

  openCreateDrawer(): void {
    this.drawerMode.set('create');
    this.drawerItem.set({
      id: '',
      section: this.section(),
      sourceKey: null,
      sourceVersion: 0,
      isCustomized: true,
      isActive: true,
      nameAr: '',
      nameEn: '',
      fields: {},
    });
  }

  openDrawer(item: TemplateItem): void {
    this.drawerMode.set('edit');
    this.drawerItem.set(item);
  }

  closeDrawer(): void {
    this.drawerItem.set(null);
    this.drawerMode.set('edit');
  }

  async saveDrawer(body: unknown): Promise<boolean> {
    const item = this.drawerItem();
    if (!item) {
      return false;
    }
    this.busy.set(true);
    this.lastError.set(null);
    try {
      if (this.drawerMode() === 'create') {
        const created = await this.api.createItem(this.section(), body);
        this.items.update((list) => [created, ...list]);
        this.totalCount.update((n) => n + 1);
      } else {
        const updated = await this.api.updateItem(this.section(), item.id, body);
        this.items.update((list) => list.map((x) => (x.id === updated.id ? updated : x)));
      }
      this.closeDrawer();
      return true;
    } catch (err) {
      this.lastError.set(mapError(err));
      return false;
    } finally {
      this.busy.set(false);
    }
  }

  async deleteItem(item: TemplateItem): Promise<boolean> {
    this.busy.set(true);
    this.lastError.set(null);
    try {
      await this.api.deleteItem(this.section(), item.id);
      this.items.update((list) => list.filter((x) => x.id !== item.id));
      this.totalCount.update((n) => Math.max(0, n - 1));
      return true;
    } catch (err) {
      this.lastError.set(mapError(err));
      return false;
    } finally {
      this.busy.set(false);
    }
  }

  async resetItem(item: TemplateItem): Promise<boolean> {
    this.busy.set(true);
    this.lastError.set(null);
    try {
      const updated = await this.api.resetItem(this.section(), item.id);
      this.items.update((list) => list.map((x) => (x.id === updated.id ? updated : x)));
      if (this.drawerItem()?.id === updated.id) {
        this.drawerItem.set(updated);
      }
      return true;
    } catch (err) {
      this.lastError.set(mapError(err));
      return false;
    } finally {
      this.busy.set(false);
    }
  }

  async toggleActive(item: TemplateItem, isActive: boolean): Promise<boolean> {
    this.busy.set(true);
    this.lastError.set(null);
    try {
      const updated = await this.api.setActive(this.section(), item.id, isActive);
      this.items.update((list) => list.map((x) => (x.id === updated.id ? updated : x)));
      return true;
    } catch (err) {
      this.lastError.set(mapError(err));
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

function mapError(err: unknown): TemplateActionError {
  const code = problemDetailsFromError(err)?.code;
  if (code === 'template.item_in_use') {
    return { kind: 'in_use' };
  }
  if (code === 'template.duplicate_name') {
    return { kind: 'duplicate' };
  }
  return { kind: 'generic' };
}
