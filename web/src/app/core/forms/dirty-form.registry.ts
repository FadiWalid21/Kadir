import { Injectable, computed, signal } from '@angular/core';

/**
 * Tracks open editors with unsaved changes so UpdateService can warn before reload.
 * Call register(id) when a form becomes dirty; clear(id) on save/discard/close.
 */
@Injectable({ providedIn: 'root' })
export class DirtyFormRegistry {
  private readonly ids = signal<ReadonlySet<string>>(new Set());

  readonly hasDirty = computed(() => this.ids().size > 0);

  register(id: string): void {
    this.ids.update((set) => {
      const next = new Set(set);
      next.add(id);
      return next;
    });
  }

  clear(id: string): void {
    this.ids.update((set) => {
      if (!set.has(id)) {
        return set;
      }
      const next = new Set(set);
      next.delete(id);
      return next;
    });
  }

  clearAll(): void {
    this.ids.set(new Set());
  }
}
