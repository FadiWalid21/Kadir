import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ButtonComponent } from '../button/button.component';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'kadr-seat-limit-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonComponent, IconComponent, RouterLink, TranslocoPipe],
  template: `
    @if (open()) {
      <div class="fixed inset-0 z-50 flex items-center justify-center p-4" role="presentation">
        <button
          type="button"
          class="absolute inset-0 bg-fg/40"
          [attr.aria-label]="'ui.cancel' | transloco"
          (click)="closed.emit()"
        ></button>
        <div
          class="relative z-10 w-full max-w-sm rounded-lg border border-line bg-surface p-4 shadow-lg"
          role="alertdialog"
          aria-modal="true"
          [attr.aria-labelledby]="titleId"
          [attr.aria-describedby]="bodyId"
        >
          <div class="flex items-start gap-3">
            <span class="mt-0.5 text-warning">
              <kadr-icon name="users" size="md" />
            </span>
            <div class="min-w-0 flex-1 text-start">
              <h2 [id]="titleId" class="text-lg font-bold text-fg">
                {{ 'subscription.seatLimit.title' | transloco }}
              </h2>
              <p [id]="bodyId" class="mt-2 text-sm text-fg-2">
                {{
                  'subscription.seatLimit.body'
                    | transloco: { cap: cap() ?? '—', active: active() }
                }}
              </p>
            </div>
          </div>
          <div class="mt-4 flex flex-wrap justify-end gap-2">
            <kadr-button variant="secondary" type="button" (click)="closed.emit()">
              {{ 'subscription.seatLimit.close' | transloco }}
            </kadr-button>
            <a routerLink="/settings/subscription" (click)="closed.emit()">
              <kadr-button variant="primary" type="button">
                {{ 'subscription.seatLimit.viewPlans' | transloco }}
              </kadr-button>
            </a>
          </div>
        </div>
      </div>
    }
  `,
})
export class SeatLimitDialogComponent {
  readonly open = input(false);
  readonly cap = input<number | null>(null);
  readonly active = input(0);
  readonly closed = output<void>();

  protected readonly titleId = `seat-title-${Math.random().toString(36).slice(2, 9)}`;
  protected readonly bodyId = `seat-body-${Math.random().toString(36).slice(2, 9)}`;
}
