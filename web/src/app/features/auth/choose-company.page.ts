import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { GoogleCompanyOption } from '../../core/api/auth.models';
import { GoogleErrorCodes, AuthService } from '../../core/auth/auth.service';
import { CompanySelectionService } from '../../core/auth/company-selection.service';
import { GoogleGisLoader } from '../../core/auth/google-gis.loader';
import { ThemeService } from '../../core/theme/theme.service';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { AuthAlertComponent } from './auth-alert.component';
import { AuthShellLayout } from './auth-shell.layout';
import {
  COMPANY_AVATAR_CLASSES,
  companyAvatarTone,
  companyInitials,
} from './company-avatar';

interface RowError {
  tenantId: string;
  expired: boolean;
}

@Component({
  selector: 'kadr-choose-company-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    RouterLink,
    ButtonComponent,
    IconComponent,
    AuthAlertComponent,
    AuthShellLayout,
  ],
  template: `
    <kadr-auth-shell
      [title]="'auth.chooseCompany.title' | transloco"
      [subtitle]="'auth.chooseCompany.subtitle' | transloco"
      [showSecureBadge]="false"
    >
      @if (state(); as current) {
        <div class="flex flex-col gap-4" data-testid="choose-company">
          @if (current.email) {
            <p
              class="inline-flex w-fit max-w-full items-center gap-2 rounded-full bg-subtle px-3 py-1 text-xs text-fg-2"
              data-testid="google-chip"
            >
              <span class="text-success"><kadr-icon name="check" size="sm" /></span>
              <bdi class="truncate">{{ current.email }}</bdi>
              <span class="shrink-0 text-fg-muted">{{ 'auth.chooseCompany.verifiedBy' | transloco }}</span>
            </p>
          }

          <p class="text-sm font-medium text-fg-2">
            {{ 'auth.chooseCompany.available' | transloco: { count: current.companies.length } }}
          </p>

          <ul class="flex flex-col gap-3" role="list">
            @for (company of current.companies; track company.tenantId) {
              <li class="flex flex-col gap-2">
                <button
                  type="button"
                  class="flex w-full items-center gap-3 rounded-md border border-line bg-surface p-3 text-start transition-colors hover:bg-hover focus-visible:outline-none disabled:opacity-50 disabled:pointer-events-none"
                  [disabled]="selectingId() !== null"
                  [attr.aria-busy]="selectingId() === company.tenantId || null"
                  [attr.data-testid]="'company-row-' + company.tenantId"
                  (click)="choose(company)"
                >
                  <span
                    class="inline-flex size-12 shrink-0 items-center justify-center rounded-md text-base font-bold"
                    [class]="avatarClass(company)"
                    aria-hidden="true"
                  >
                    {{ initials(company) }}
                  </span>
                  <span class="flex min-w-0 flex-1 flex-col gap-1">
                    <span class="break-words text-base font-bold text-fg">{{ company.name }}</span>
                    <span class="flex flex-wrap items-center gap-x-3 gap-y-1">
                      <span
                        class="inline-flex h-6 items-center rounded-full bg-subtle px-2.5 text-xs font-medium text-fg-2"
                      >
                        {{ company.roleName }}
                      </span>
                      <span class="text-xs text-fg-muted">{{ lastLogin(company) }}</span>
                    </span>
                  </span>
                  @if (selectingId() === company.tenantId) {
                    <span
                      class="inline-block size-5 shrink-0 animate-spin rounded-full border-2 border-accent border-e-transparent"
                      role="status"
                      [attr.aria-label]="'auth.chooseCompany.entering' | transloco"
                    ></span>
                  } @else {
                    <span class="shrink-0 text-fg-muted">
                      <kadr-icon name="chevron-end" size="md" [rtlFlip]="true" />
                    </span>
                  }
                </button>

                @if (rowError(); as err) {
                  @if (err.tenantId === company.tenantId) {
                    <kadr-auth-alert
                      tone="danger"
                      icon="alert"
                      [title]="'auth.chooseCompany.error.title' | transloco"
                      [message]="
                        (err.expired
                          ? 'auth.chooseCompany.error.expired'
                          : 'auth.chooseCompany.error.body'
                        ) | transloco
                      "
                      data-testid="row-error"
                    >
                      @if (err.expired) {
                        <a
                          routerLink="/login"
                          class="font-medium text-accent underline-offset-2 hover:underline"
                          (click)="selection.clear()"
                        >
                          {{ 'auth.backToLogin' | transloco }}
                        </a>
                      } @else {
                        <button
                          type="button"
                          class="font-medium text-accent underline-offset-2 hover:underline focus-visible:outline-none"
                          data-testid="row-retry"
                          (click)="choose(company)"
                        >
                          {{ 'auth.chooseCompany.error.retry' | transloco }}
                        </button>
                      }
                    </kadr-auth-alert>
                  }
                }
              </li>
            }
          </ul>

          <kadr-button
            variant="ghost"
            type="button"
            class="w-fit"
            [disabled]="selectingId() !== null"
            (click)="useAnotherAccount()"
          >
            <kadr-icon name="logout" size="sm" [rtlFlip]="true" />
            {{ 'auth.chooseCompany.useAnother' | transloco }}
          </kadr-button>
        </div>
      }

      <div authFooter class="text-start text-sm">
        <a routerLink="/login" class="text-accent underline-offset-2 hover:underline" (click)="selection.clear()">
          {{ 'auth.backToLogin' | transloco }}
        </a>
      </div>
    </kadr-auth-shell>
  `,
})
export class ChooseCompanyPage implements OnInit {
  protected readonly selection = inject(CompanySelectionService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly gis = inject(GoogleGisLoader);
  private readonly theme = inject(ThemeService);
  private readonly transloco = inject(TranslocoService);

  protected readonly state = this.selection.state;
  protected readonly selectingId = signal<string | null>(null);
  protected readonly rowError = signal<RowError | null>(null);

  private readonly dateFormat = computed(
    () => new Intl.DateTimeFormat(this.theme.language(), { dateStyle: 'medium' }),
  );

  ngOnInit(): void {
    // No in-memory state (reload, direct visit): back to the login page.
    if (!this.selection.state()) {
      void this.router.navigateByUrl('/login', { replaceUrl: true });
    }
  }

  protected initials(company: GoogleCompanyOption): string {
    return companyInitials(company.name);
  }

  protected avatarClass(company: GoogleCompanyOption): string {
    return COMPANY_AVATAR_CLASSES[companyAvatarTone(company.tenantId)];
  }

  protected lastLogin(company: GoogleCompanyOption): string {
    const parsed = company.lastLoginAt ? new Date(company.lastLoginAt) : null;
    if (!parsed || Number.isNaN(parsed.getTime())) {
      return this.transloco.translate('auth.chooseCompany.neverSignedIn');
    }
    return this.transloco.translate('auth.chooseCompany.lastLogin', {
      date: this.dateFormat().format(parsed),
    });
  }

  async choose(company: GoogleCompanyOption): Promise<void> {
    const current = this.selection.state();
    if (!current || this.selectingId() !== null) {
      return;
    }
    this.rowError.set(null);
    this.selectingId.set(company.tenantId);
    try {
      await this.auth.selectGoogleTenant(current.selectionToken, company.tenantId);
      this.selection.clear();
      await this.router.navigateByUrl('/');
    } catch (error) {
      const code = problemDetailsFromError(error)?.code;
      this.rowError.set({
        tenantId: company.tenantId,
        expired: code === GoogleErrorCodes.selectionInvalid,
      });
    } finally {
      this.selectingId.set(null);
    }
  }

  async useAnotherAccount(): Promise<void> {
    this.selection.clear();
    this.gis.disableAutoSelect();
    await this.router.navigateByUrl('/login');
  }
}
