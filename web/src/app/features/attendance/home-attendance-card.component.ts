import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AttendanceApi, AttendanceToday } from '../../core/api/attendance.api';
import { GeolocationService } from '../../core/geo/geolocation.service';
import { OfflineService } from '../../core/pwa/offline.service';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

type CardUi =
  | 'skeleton'
  | 'ready'
  | 'locating'
  | 'outside'
  | 'permission'
  | 'denied'
  | 'clockedIn'
  | 'earlyOut'
  | 'complete'
  | 'holiday'
  | 'weekend'
  | 'noShift'
  | 'offline'
  | 'branchMissing'
  | 'error'
  | 'noEmployee';

@Component({
  selector: 'kadr-home-attendance-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    ButtonComponent,
    BadgeComponent,
    IconComponent,
    SkeletonComponent,
    DrawerComponent,
  ],
  template: `
    @if (ui() === 'skeleton') {
      <div
        class="flex flex-col gap-3 rounded-lg border border-line bg-app p-4"
        role="status"
        [attr.aria-label]="'ui.loading' | transloco"
      >
        <kadr-skeleton height="1.25rem" width="45%" />
        <kadr-skeleton height="3.5rem" />
        <kadr-skeleton height="2.75rem" />
      </div>
    } @else if (ui() === 'offline') {
      <p class="rounded-lg border border-line bg-warning-soft p-4 text-sm text-fg">
        {{ 'attendance.card.offline' | transloco }}
      </p>
    } @else if (ui() === 'noEmployee') {
      <p class="rounded-lg border border-line bg-app p-4 text-sm text-fg-2">
        {{ 'attendance.card.noEmployee' | transloco }}
      </p>
    } @else if (ui() === 'error') {
      <div class="flex flex-col gap-3 rounded-lg border border-line bg-app p-4 text-start">
        <p class="text-sm text-fg-2">{{ 'attendance.card.loadError' | transloco }}</p>
        <kadr-button variant="secondary" type="button" (click)="reload()">
          {{ 'pwa.offline.retry' | transloco }}
        </kadr-button>
      </div>
    } @else if (ui() === 'holiday' || ui() === 'weekend' || ui() === 'noShift') {
      <div class="flex flex-col gap-2 rounded-lg border border-line bg-app p-4 text-start">
        <p class="text-sm font-medium text-fg">{{ dayKindLabel() }}</p>
        <p class="text-sm text-fg-2">{{ 'attendance.card.nonWorkdayHint' | transloco }}</p>
      </div>
    } @else if (ui() === 'branchMissing') {
      <p class="rounded-lg border border-line bg-app p-4 text-sm text-fg-2">
        {{ 'attendance.card.branchMissing' | transloco }}
      </p>
    } @else if (ui() === 'permission') {
      <div class="flex flex-col gap-3 rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
        <p class="text-sm text-fg-2">{{ 'attendance.card.permissionExplain' | transloco }}</p>
        <kadr-button variant="primary" type="button" (click)="onPunch()">
          {{ punchLabel() }}
        </kadr-button>
      </div>
    } @else if (ui() === 'denied') {
      <p class="rounded-lg border border-line bg-danger-soft p-4 text-sm text-fg">
        {{ 'attendance.card.permissionDenied' | transloco }}
      </p>
    } @else if (ui() === 'complete') {
      <div class="flex flex-col gap-2 rounded-lg border border-line bg-success-soft p-4 text-start">
        <div class="flex items-center gap-2">
          <kadr-icon name="check" size="sm" class="text-success" />
          <p class="text-sm font-medium text-fg">{{ 'attendance.card.dayComplete' | transloco }}</p>
        </div>
        <p class="text-sm text-fg-2">{{ workedSummary() }}</p>
      </div>
    } @else if (ui() === 'clockedIn' || ui() === 'locating' || ui() === 'ready' || ui() === 'outside' || ui() === 'earlyOut') {
      <div class="flex flex-col gap-4 rounded-lg border border-line bg-surface p-4 text-start shadow-sm sm:p-5">
        @if (ui() === 'clockedIn' || ui() === 'outside') {
          <div
            class="flex items-center gap-2 rounded-md px-3 py-2 text-sm"
            [class.bg-success-soft]="ui() !== 'outside'"
            [class.text-success]="ui() !== 'outside'"
            [class.bg-warning-soft]="ui() === 'outside'"
            [class.text-warning]="ui() === 'outside'"
            role="status"
          >
            <kadr-icon name="info" size="sm" />
            <span>{{
              (ui() === 'outside' ? 'attendance.card.outOfRangeHint' : 'attendance.card.inRange')
                | transloco
            }}</span>
          </div>
        }

        @if (today()?.branch?.name) {
          <p class="text-xs text-fg-muted">
            {{ 'attendance.card.branchLabel' | transloco }}:
            <span class="font-medium text-fg">{{ today()!.branch.name }}</span>
          </p>
        }

        @if (shiftLine()) {
          <p class="text-sm text-fg-2">{{ shiftLine() }}</p>
        }

        @if (ui() === 'clockedIn') {
          <div class="flex flex-col items-center gap-2 py-2">
            <p class="text-3xl font-bold tabular-nums tracking-wide text-fg sm:text-4xl">
              {{ elapsedLabel() }}
            </p>
            @if (today()?.openPunch; as punch) {
              <p class="text-xs text-fg-muted">
                {{ 'attendance.card.checkInAt' | transloco }}:
                <span class="tabular-nums text-fg-2">{{ formatTime(punch.punchedAt) }}</span>
              </p>
            }
            @if ((today()?.lateMinutes ?? 0) > 0) {
              <kadr-badge
                tone="warning"
                [label]="'attendance.card.lateChip' | transloco: { minutes: today()!.lateMinutes }"
              />
            }
          </div>
        }

        @if (previewDistanceM() !== null) {
          <p class="text-xs text-fg-muted">
            {{ 'attendance.card.previewDistance' | transloco: { meters: previewDistanceM() } }}
          </p>
        }

        <kadr-button
          class="w-full"
          [variant]="ui() === 'clockedIn' ? 'secondary' : 'primary'"
          type="button"
          [disabled]="ui() === 'locating' || offline.offline()"
          (click)="ui() === 'clockedIn' ? openEarlyOut() : onPunch()"
        >
          @if (ui() === 'locating') {
            {{ 'attendance.card.locating' | transloco }}
          } @else {
            {{ punchLabel() }}
          }
        </kadr-button>
      </div>
    }

    <kadr-drawer
      [open]="outsideOpen()"
      [title]="'attendance.card.outsideTitle' | transloco"
      [closeLabel]="'ui.close' | transloco"
      (closed)="outsideOpen.set(false)"
    >
      <div class="flex flex-col gap-3 p-4 text-start">
        <p class="text-sm text-fg-2">{{ 'attendance.card.outsideBody' | transloco }}</p>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.card.reasonLabel' | transloco }}</span>
          <textarea
            class="min-h-24 rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [value]="outsideReason()"
            (input)="outsideReason.set($any($event.target).value)"
          ></textarea>
        </label>
        <kadr-button variant="primary" type="button" (click)="confirmOutside()">
          {{ 'attendance.card.confirmOutside' | transloco }}
        </kadr-button>
      </div>
    </kadr-drawer>

    <kadr-drawer
      [open]="earlyOutOpen()"
      [title]="'attendance.card.earlyOutTitle' | transloco"
      [closeLabel]="'ui.close' | transloco"
      (closed)="earlyOutOpen.set(false)"
    >
      <div class="flex flex-col gap-3 p-4 text-start">
        <p class="text-sm text-fg-2">{{ 'attendance.card.earlyOutBody' | transloco }}</p>
        <kadr-button variant="danger-soft" type="button" (click)="confirmEarlyOut()">
          {{ 'glossary.clockOut' | transloco }}
        </kadr-button>
      </div>
    </kadr-drawer>
  `,
})
export class HomeAttendanceCardComponent implements OnInit, OnDestroy {
  private readonly api = inject(AttendanceApi);
  private readonly geo = inject(GeolocationService);
  private readonly toast = inject(ToastService);
  private readonly transloco = inject(TranslocoService);
  protected readonly offline = inject(OfflineService);

  protected readonly today = signal<AttendanceToday | null>(null);
  protected readonly ui = signal<CardUi>('skeleton');
  protected readonly outsideOpen = signal(false);
  protected readonly earlyOutOpen = signal(false);
  protected readonly outsideReason = signal('');
  protected readonly previewDistanceM = signal<number | null>(null);
  protected readonly elapsedLabel = signal('00:00:00');

  private timer: ReturnType<typeof setInterval> | null = null;
  private pendingFix: { latitude: number; longitude: number; accuracyM: number; positionAgeMs: number } | null =
    null;

  protected readonly dayKindLabel = computed(() => {
    const kind = this.today()?.dayKind;
    if (kind === 'Holiday') return this.transloco.translate('attendance.day.Holiday');
    if (kind === 'Weekend') return this.transloco.translate('attendance.day.Weekend');
    if (kind === 'NoShift') return this.transloco.translate('attendance.day.NoShift');
    return '';
  });

  ngOnInit(): void {
    void this.reload();
  }

  ngOnDestroy(): void {
    if (this.timer) {
      clearInterval(this.timer);
    }
  }

  protected async reload(): Promise<void> {
    if (this.offline.offline()) {
      this.ui.set('offline');
      return;
    }
    this.ui.set('skeleton');
    try {
      const data = await this.api.getMyToday();
      this.today.set(data);
      this.applyToday(data);
      void this.maybePreview(data);
    } catch (err) {
      const problem = problemDetailsFromError(err);
      if (problem?.code === 'attendance.no_employee') {
        this.ui.set('noEmployee');
        return;
      }
      this.ui.set('error');
    }
  }

  protected punchLabel(): string {
    return this.today()?.openPunch
      ? this.transloco.translate('glossary.clockOut')
      : this.transloco.translate('glossary.clockIn');
  }

  protected shiftLine(): string {
    const s = this.today()?.shift;
    if (!s?.startTime || !s.endTime) {
      return '';
    }
    const name = this.transloco.getActiveLang() === 'en' ? s.nameEn ?? s.nameAr : s.nameAr ?? s.nameEn;
    return this.transloco.translate('attendance.card.shiftLine', {
      name: name ?? '',
      start: s.startTime.slice(0, 5),
      end: s.endTime.slice(0, 5),
    });
  }

  protected workedSummary(): string {
    const punches = this.today()?.punches ?? [];
    const inn = punches.find((p) => p.kind === 'In');
    const out = [...punches].reverse().find((p) => p.kind === 'Out');
    if (!inn || !out) {
      return '';
    }
    return this.transloco.translate('attendance.card.workedRange', {
      inn: this.formatTime(inn.punchedAt),
      out: this.formatTime(out.punchedAt),
    });
  }

  protected openEarlyOut(): void {
    this.earlyOutOpen.set(true);
  }

  protected async confirmEarlyOut(): Promise<void> {
    this.earlyOutOpen.set(false);
    await this.onPunch(true);
  }

  protected async confirmOutside(): Promise<void> {
    const reason = this.outsideReason().trim();
    if (!reason) {
      this.toast.show(this.transloco.translate('attendance.card.reasonRequired'), 'danger');
      return;
    }
    this.outsideOpen.set(false);
    await this.submitPunch(reason);
  }

  protected async onPunch(skipEarlyConfirm = false): Promise<void> {
    if (this.offline.offline()) {
      this.ui.set('offline');
      return;
    }
    if (this.today()?.openPunch && !skipEarlyConfirm) {
      this.openEarlyOut();
      return;
    }

    const perm = await this.geo.getPermissionState();
    if (perm === 'unsupported') {
      this.toast.show(this.transloco.translate('attendance.card.unsupported'), 'danger');
      return;
    }
    if (perm === 'denied') {
      this.ui.set('denied');
      return;
    }
    if (perm === 'prompt') {
      this.ui.set('permission');
    }

    this.ui.set('locating');
    const maxAcc = 100;
    const located = await this.geo.locateForPunch(maxAcc);
    if (!located.ok) {
      if (located.reason === 'denied') {
        this.ui.set('denied');
      } else {
        this.toast.show(this.transloco.translate('attendance.card.locateFailed'), 'danger');
        this.applyToday(this.today()!);
      }
      return;
    }

    this.pendingFix = located.fix;
    await this.submitPunch();
  }

  private async submitPunch(reason?: string): Promise<void> {
    if (!this.pendingFix) {
      return;
    }
    try {
      await this.api.punch({
        latitude: this.pendingFix.latitude,
        longitude: this.pendingFix.longitude,
        accuracyM: this.pendingFix.accuracyM,
        positionAgeMs: this.pendingFix.positionAgeMs,
        clientRequestId: crypto.randomUUID(),
        reason: reason ?? null,
      });
      this.pendingFix = null;
      this.toast.show(this.transloco.translate('attendance.card.punchOk'), 'success');
      await this.reload();
    } catch (err) {
      const code = problemDetailsFromError(err)?.code ?? '';
      if (code === 'attendance.reason_required' || code === 'attendance.outside_range') {
        this.outsideOpen.set(true);
        this.ui.set('outside');
        return;
      }
      if (code === 'attendance.branch_not_configured') {
        this.ui.set('branchMissing');
        return;
      }
      this.toast.show(this.transloco.translate('attendance.card.punchFailed'), 'danger');
      this.applyToday(this.today()!);
    }
  }

  private applyToday(data: AttendanceToday): void {
    if (!data.canPunch && data.dayKind === 'Workday' && !data.branch.isConfigured) {
      this.ui.set('branchMissing');
      return;
    }
    if (data.dayKind === 'Holiday') {
      this.ui.set('holiday');
      return;
    }
    if (data.dayKind === 'Weekend') {
      this.ui.set('weekend');
      return;
    }
    if (data.dayKind === 'NoShift') {
      this.ui.set('noShift');
      return;
    }

    const hasIn = data.punches.some((p) => p.kind === 'In');
    const hasOut = data.punches.some((p) => p.kind === 'Out');
    if (hasIn && hasOut && !data.openPunch) {
      this.ui.set('complete');
      this.stopTimer();
      return;
    }
    if (data.openPunch) {
      this.ui.set('clockedIn');
      this.startTimer(data.openPunch.punchedAt);
      return;
    }
    this.ui.set('ready');
    this.stopTimer();
  }

  private async maybePreview(data: AttendanceToday): Promise<void> {
    if (!data.branch.isConfigured || data.branch.latitude == null || data.branch.longitude == null) {
      return;
    }
    const fix = await this.geo.tryPreviewFix();
    if (!fix) {
      return;
    }
    const meters = this.haversine(
      data.branch.latitude,
      data.branch.longitude,
      fix.latitude,
      fix.longitude,
    );
    this.previewDistanceM.set(Math.round(meters));
  }

  private startTimer(fromIso: string): void {
    this.stopTimer();
    const start = new Date(fromIso).getTime();
    const tick = () => {
      const sec = Math.max(0, Math.floor((Date.now() - start) / 1000));
      const h = String(Math.floor(sec / 3600)).padStart(2, '0');
      const m = String(Math.floor((sec % 3600) / 60)).padStart(2, '0');
      const s = String(sec % 60).padStart(2, '0');
      this.elapsedLabel.set(`${h}:${m}:${s}`);
    };
    tick();
    this.timer = setInterval(tick, 1000);
  }

  private stopTimer(): void {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }

  protected formatTime(iso: string): string {
    try {
      return new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
    } catch {
      return iso;
    }
  }

  private haversine(lat1: number, lon1: number, lat2: number, lon2: number): number {
    const R = 6_371_000;
    const toRad = (d: number) => (d * Math.PI) / 180;
    const dLat = toRad(lat2 - lat1);
    const dLon = toRad(lon2 - lon1);
    const a =
      Math.sin(dLat / 2) ** 2 +
      Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLon / 2) ** 2;
    return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  }
}
