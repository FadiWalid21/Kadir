import {
  ChangeDetectionStrategy,
  Component,
  computed,
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
import {
  AttendanceApi,
  UpsertWorkLocationBody,
  WorkLocation,
} from '../../core/api/attendance.api';
import { GeolocationService } from '../../core/geo/geolocation.service';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

@Component({
  selector: 'kadr-work-location-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, TranslocoPipe, DrawerComponent, ButtonComponent],
  template: `
    <kadr-drawer
      [open]="isOpen()"
      [title]="
        (creating() ? 'attendance.locations.createTitle' : 'attendance.locations.editTitle')
          | transloco
      "
      [closeLabel]="'ui.close' | transloco"
      (closed)="closed.emit()"
    >
      <div class="flex flex-col gap-3 p-4 text-start">
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.locations.nameAr' | transloco }}</span>
          <input
            type="text"
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="nameAr"
            name="nameAr"
          />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.locations.nameEn' | transloco }}</span>
          <input
            type="text"
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="nameEn"
            name="nameEn"
          />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.locations.type' | transloco }}</span>
          <select
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="type"
            name="type"
          >
            <option value="Branch">{{ 'attendance.locations.typeBranch' | transloco }}</option>
            <option value="ExternalSite">{{ 'attendance.locations.typeExternal' | transloco }}</option>
          </select>
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.locations.address' | transloco }}</span>
          <input
            type="text"
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="address"
            name="address"
          />
        </label>
        <div class="grid grid-cols-2 gap-2">
          <label class="flex flex-col gap-1 text-sm">
            <span class="text-fg-2">{{ 'attendance.settings.latitude' | transloco }}</span>
            <input
              type="number"
              step="any"
              class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
              [(ngModel)]="latitude"
              name="lat"
              (ngModelChange)="syncMap()"
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
              (ngModelChange)="syncMap()"
            />
          </label>
        </div>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.settings.radius' | transloco }}</span>
          <input
            type="range"
            min="50"
            max="1000"
            step="10"
            class="w-full"
            [(ngModel)]="radius"
            name="radiusRange"
            (ngModelChange)="syncMap()"
          />
          <input
            type="number"
            min="50"
            max="1000"
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="radius"
            name="radius"
            (ngModelChange)="syncMap()"
          />
        </label>
        <div class="grid grid-cols-2 gap-2">
          <label class="flex flex-col gap-1 text-sm">
            <span class="text-fg-2">{{ 'attendance.locations.validFrom' | transloco }}</span>
            <input
              type="date"
              class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
              [(ngModel)]="validFrom"
              name="validFrom"
            />
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span class="text-fg-2">{{ 'attendance.locations.validTo' | transloco }}</span>
            <input
              type="date"
              class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
              [(ngModel)]="validTo"
              name="validTo"
            />
          </label>
        </div>
        <p class="text-xs text-fg-muted">{{ 'attendance.locations.validHint' | transloco }}</p>
        <label class="flex items-center gap-2 text-sm text-fg">
          <input type="checkbox" [(ngModel)]="isActive" name="isActive" />
          {{ 'attendance.locations.active' | transloco }}
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
export class WorkLocationDrawerComponent {
  private readonly api = inject(AttendanceApi);
  private readonly geo = inject(GeolocationService);
  private readonly toast = inject(ToastService);
  private readonly transloco = inject(TranslocoService);

  readonly location = input<WorkLocation | null>(null);
  readonly creating = input(false);
  readonly closed = output<void>();
  readonly saved = output<WorkLocation>();

  private readonly mapHost = viewChild<ElementRef<HTMLElement>>('mapHost');

  protected nameAr = '';
  protected nameEn = '';
  protected type = 'ExternalSite';
  protected address = '';
  protected latitude = 30.0444;
  protected longitude = 31.2357;
  protected radius = 150;
  protected validFrom = '';
  protected validTo = '';
  protected isActive = true;
  protected readonly saving = signal(false);
  protected readonly isOpen = computed(() => this.creating() || !!this.location());

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private map: any = null;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private marker: any = null;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private circle: any = null;

  constructor() {
    effect(() => {
      const creating = this.creating();
      const loc = this.location();
      if (!creating && !loc) {
        this.destroyMap();
        return;
      }
      if (loc) {
        this.nameAr = loc.nameAr;
        this.nameEn = loc.nameEn ?? '';
        this.type = loc.type;
        this.address = loc.address ?? '';
        this.latitude = loc.latitude;
        this.longitude = loc.longitude;
        this.radius = loc.geofenceRadiusMeters;
        this.validFrom = loc.validFrom ?? '';
        this.validTo = loc.validTo ?? '';
        this.isActive = loc.isActive;
      } else {
        this.nameAr = '';
        this.nameEn = '';
        this.type = 'ExternalSite';
        this.address = '';
        this.latitude = 30.0444;
        this.longitude = 31.2357;
        this.radius = 150;
        this.validFrom = '';
        this.validTo = '';
        this.isActive = true;
      }
      queueMicrotask(() => void this.ensureMap());
    });
  }

  protected syncMap(): void {
    void this.ensureMap();
  }

  protected async useCurrent(): Promise<void> {
    const result = await this.geo.locateForPunch(500);
    if (!result.ok) {
      this.toast.show(this.transloco.translate('attendance.card.locateFailed'), 'danger');
      return;
    }
    this.latitude = result.fix.latitude;
    this.longitude = result.fix.longitude;
    await this.ensureMap();
  }

  protected async save(): Promise<void> {
    if (!this.nameAr.trim()) {
      this.toast.show(this.transloco.translate('attendance.locations.nameRequired'), 'danger');
      return;
    }
    this.saving.set(true);
    const body: UpsertWorkLocationBody = {
      nameAr: this.nameAr.trim(),
      nameEn: this.nameEn.trim() || null,
      type: this.type,
      latitude: this.latitude,
      longitude: this.longitude,
      geofenceRadiusMeters: this.radius,
      address: this.address.trim() || null,
      validFrom: this.validFrom || null,
      validTo: this.validTo || null,
      isActive: this.isActive,
    };
    try {
      const loc = this.location();
      const saved = loc
        ? await this.api.updateWorkLocation(loc.id, body)
        : await this.api.createWorkLocation(body);
      this.toast.show(this.transloco.translate('attendance.settings.saved'), 'success');
      this.saved.emit(saved);
    } catch (err) {
      const pd = problemDetailsFromError(err);
      this.toast.show(
        pd?.detail ?? this.transloco.translate('attendance.settings.saveFailed'),
        'danger',
      );
    } finally {
      this.saving.set(false);
    }
  }

  private async ensureMap(): Promise<void> {
    const host = this.mapHost()?.nativeElement;
    if (!host || !(this.creating() || this.location())) {
      return;
    }
    const L = await import('leaflet');
    if (!this.map) {
      this.map = L.map(host).setView([this.latitude, this.longitude], 15);
      L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap',
        maxZoom: 19,
      }).addTo(this.map);
      this.marker = L.marker([this.latitude, this.longitude], { draggable: true }).addTo(this.map);
      this.marker.on('dragend', () => {
        const ll = this.marker.getLatLng();
        this.latitude = ll.lat;
        this.longitude = ll.lng;
        this.circle?.setLatLng(ll);
      });
      this.circle = L.circle([this.latitude, this.longitude], {
        radius: this.radius,
        color: 'var(--accent)',
        fillOpacity: 0.12,
      }).addTo(this.map);
      setTimeout(() => this.map?.invalidateSize(), 80);
    } else {
      this.map.setView([this.latitude, this.longitude]);
      this.marker?.setLatLng([this.latitude, this.longitude]);
      this.circle?.setLatLng([this.latitude, this.longitude]);
      this.circle?.setRadius(this.radius);
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
}
