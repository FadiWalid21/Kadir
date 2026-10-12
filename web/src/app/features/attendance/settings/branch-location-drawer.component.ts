import {
  ChangeDetectionStrategy,
  Component,
  effect,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AttendanceApi, BranchLocation } from '../../../core/api/attendance.api';
import { GeolocationService } from '../../../core/geo/geolocation.service';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { DrawerComponent } from '../../../shared/ui/drawer/drawer.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';

@Component({
  selector: 'kadr-branch-location-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, TranslocoPipe, DrawerComponent, ButtonComponent],
  template: `
    <kadr-drawer
      [open]="!!branch()"
      [title]="'attendance.settings.locationTitle' | transloco"
      [closeLabel]="'ui.close' | transloco"
      (closed)="closed.emit()"
    >
      <div class="flex flex-col gap-3 p-4 text-start">
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.settings.latitude' | transloco }}</span>
          <input
            type="number"
            step="any"
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="latitude"
            name="lat"
          />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.settings.longitude' | transloco }}</span>
          <input
            type="number"
            step="any"
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="longitude"
            name="lng"
          />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.settings.radius' | transloco }}</span>
          <input
            type="number"
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="radius"
            name="radius"
          />
          @if (radius < 100) {
            <span class="text-xs text-fg-muted">{{ 'attendance.settings.radiusWarn' | transloco }}</span>
          }
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.settings.outOfRangePolicy' | transloco }}</span>
          <select
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="policy"
            name="policy"
          >
            <option value="Block">{{ 'attendance.settings.policyBlock' | transloco }}</option>
            <option value="AllowWithReason">{{ 'attendance.settings.policyAllow' | transloco }}</option>
          </select>
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.settings.pasteMaps' | transloco }}</span>
          <input
            type="text"
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [ngModel]="paste()"
            (ngModelChange)="onPaste($event)"
            name="paste"
          />
        </label>
        <div class="flex flex-wrap gap-2">
          <kadr-button variant="secondary" type="button" (click)="useCurrent()">
            {{ 'attendance.settings.useCurrent' | transloco }}
          </kadr-button>
        </div>
        <div
          #mapHost
          class="h-56 w-full overflow-hidden rounded-md border border-line bg-app"
          [attr.aria-label]="'attendance.settings.mapAria' | transloco"
        ></div>
        <p class="text-xs text-fg-muted">{{ 'attendance.settings.mapAttribution' | transloco }}</p>
        <kadr-button variant="primary" type="button" [loading]="saving()" (click)="save()">
          {{ 'ui.save' | transloco }}
        </kadr-button>
      </div>
    </kadr-drawer>
  `,
})
export class BranchLocationDrawerComponent {
  private readonly api = inject(AttendanceApi);
  private readonly geo = inject(GeolocationService);
  private readonly toast = inject(ToastService);
  private readonly transloco = inject(TranslocoService);

  readonly branch = input<BranchLocation | null>(null);
  readonly closed = output<void>();
  readonly saved = output<BranchLocation>();

  private readonly mapHost = viewChild<ElementRef<HTMLElement>>('mapHost');

  protected latitude = 30.0444;
  protected longitude = 31.2357;
  protected radius = 150;
  protected policy = 'Block';
  protected readonly paste = signal('');
  protected readonly saving = signal(false);

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private map: any = null;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private marker: any = null;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private circle: any = null;

  constructor() {
    effect(() => {
      const b = this.branch();
      if (!b) {
        this.destroyMap();
        return;
      }
      this.latitude = b.latitude ?? 30.0444;
      this.longitude = b.longitude ?? 31.2357;
      this.radius = b.geofenceRadiusMeters ?? 150;
      this.policy = b.outOfRangePolicy || 'Block';
      queueMicrotask(() => void this.ensureMap());
    });
  }

  protected onPaste(value: string): void {
    this.paste.set(value);
    const parsed = this.parseLatLng(value);
    if (parsed) {
      this.latitude = parsed.lat;
      this.longitude = parsed.lng;
      void this.ensureMap();
    }
  }

  protected async useCurrent(): Promise<void> {
    const result = await this.geo.locateForPunch(500);
    if (!result.ok) {
      this.toast.show(this.transloco.translate('attendance.card.locateFailed'), 'danger');
      return;
    }
    this.latitude = result.fix.latitude;
    this.longitude = result.fix.longitude;
    void this.ensureMap();
  }

  protected async save(): Promise<void> {
    const b = this.branch();
    if (!b) {
      return;
    }
    this.saving.set(true);
    try {
      const updated = await this.api.updateBranchLocation(b.branchId, {
        latitude: Number(this.latitude),
        longitude: Number(this.longitude),
        geofenceRadiusMeters: Number(this.radius),
        outOfRangePolicy: this.policy,
      });
      this.toast.show(this.transloco.translate('attendance.settings.saved'), 'success');
      this.saved.emit(updated);
    } catch {
      this.toast.show(this.transloco.translate('attendance.settings.saveFailed'), 'danger');
    } finally {
      this.saving.set(false);
    }
  }

  private async ensureMap(): Promise<void> {
    const host = this.mapHost()?.nativeElement;
    if (!host || !this.branch()) {
      return;
    }
    try {
      const L = await import('leaflet');
      const accent =
        getComputedStyle(document.documentElement).getPropertyValue('--brand-base').trim() ||
        getComputedStyle(document.documentElement).getPropertyValue('--color-accent').trim() ||
        'currentColor';
      if (!this.map) {
        this.map = L.map(host).setView([this.latitude, this.longitude], 16);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
          attribution: '&copy; OpenStreetMap',
          maxZoom: 19,
        }).addTo(this.map);
        this.marker = L.circleMarker([this.latitude, this.longitude], {
          radius: 8,
          color: accent,
          fillColor: accent,
          fillOpacity: 0.9,
        }).addTo(this.map);
        this.map.on('click', (e: { latlng: { lat: number; lng: number } }) => {
          this.latitude = e.latlng.lat;
          this.longitude = e.latlng.lng;
          this.marker.setLatLng([this.latitude, this.longitude]);
          this.syncCircle();
        });
        this.circle = L.circle([this.latitude, this.longitude], {
          radius: this.radius,
          color: accent,
        }).addTo(this.map);
      } else {
        this.map.setView([this.latitude, this.longitude], 16);
        this.marker.setLatLng([this.latitude, this.longitude]);
        this.syncCircle();
      }
      setTimeout(() => this.map?.invalidateSize(), 100);
    } catch {
      // Map optional — form fields still work
    }
  }

  private syncCircle(): void {
    if (this.circle) {
      this.circle.setLatLng([this.latitude, this.longitude]);
      this.circle.setRadius(Number(this.radius) || 150);
    }
  }

  private destroyMap(): void {
    if (this.map) {
      this.map.remove();
      this.map = null;
      this.marker = null;
      this.circle = null;
    }
  }

  private parseLatLng(raw: string): { lat: number; lng: number } | null {
    const text = raw.trim();
    const pair = text.match(/(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)/);
    if (pair) {
      return { lat: Number(pair[1]), lng: Number(pair[2]) };
    }
    const maps = text.match(/@(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)/);
    if (maps) {
      return { lat: Number(maps[1]), lng: Number(maps[2]) };
    }
    return null;
  }
}
