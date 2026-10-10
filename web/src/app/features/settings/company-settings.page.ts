import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { TenantApi } from '../../core/api/tenant.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { TenantLogoComponent } from '../../shared/ui/tenant-logo/tenant-logo.component';
import { SettingsSubnavComponent } from './settings-subnav.component';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-company-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    ButtonComponent,
    InputComponent,
    SkeletonComponent,
    EmptyStateComponent,
    TenantLogoComponent,
    SettingsSubnavComponent,
  ],
  template: `
    @if (state() === 'loading') {
      <div class="flex flex-col gap-4" role="status" [attr.aria-label]="'ui.loading' | transloco">
        <kadr-skeleton height="2rem" width="40%" />
        <kadr-skeleton height="12rem" />
      </div>
    } @else if (state() === 'error') {
      <kadr-empty-state
        [title]="'settings.company.loadErrorTitle' | transloco"
        [description]="'settings.company.loadErrorBody' | transloco"
      >
        <kadr-button variant="secondary" type="button" (click)="reload()">
          {{ 'home.retry' | transloco }}
        </kadr-button>
      </kadr-empty-state>
    } @else {
      <div class="mx-auto flex w-full max-w-xl flex-col gap-6">
        <kadr-settings-subnav active="company" [showTemplates]="showTemplates()" />
        <header class="flex flex-col gap-2 text-start">
          <h1 class="text-2xl font-bold leading-tight text-fg">
            {{ 'settings.company.title' | transloco }}
          </h1>
          <p class="text-sm text-fg-2">{{ 'settings.company.subtitle' | transloco }}</p>
        </header>

        <section
          class="flex flex-col gap-5 rounded-lg border border-line bg-surface p-5 shadow-sm sm:p-6"
          [attr.aria-label]="'settings.company.identityAria' | transloco"
        >
          <h2 class="text-lg font-bold text-fg">{{ 'settings.company.identityTitle' | transloco }}</h2>

          <div class="flex flex-wrap items-center gap-4">
            <div
              class="flex size-20 items-center justify-center overflow-hidden rounded-md border border-line bg-subtle"
            >
              <kadr-tenant-logo
                [logoUrl]="logoUrl()"
                [logoVersion]="logoVersion()"
                [fallbackName]="name()"
                size="lg"
              />
            </div>
            <div class="flex min-w-0 flex-1 flex-col gap-2 text-start">
              <p class="text-sm text-fg-2">{{ 'settings.company.logoHint' | transloco }}</p>
              <div class="flex flex-wrap gap-2">
                <label class="inline-flex">
                  <input
                    #fileInput
                    type="file"
                    class="sr-only"
                    accept="image/png,image/jpeg,image/svg+xml,.png,.jpg,.jpeg,.svg"
                    [disabled]="!canUpdate() || logoBusy()"
                    (change)="onLogoSelected($event)"
                  />
                  <kadr-button
                    variant="secondary"
                    type="button"
                    [disabled]="!canUpdate() || logoBusy()"
                    [loading]="logoBusy()"
                    (click)="fileInput.click()"
                  >
                    {{ 'settings.company.changeLogo' | transloco }}
                  </kadr-button>
                </label>
                @if (logoUrl()) {
                  <kadr-button
                    variant="ghost"
                    type="button"
                    [disabled]="!canUpdate() || logoBusy()"
                    (click)="removeLogo()"
                  >
                    {{ 'settings.company.removeLogo' | transloco }}
                  </kadr-button>
                }
              </div>
            </div>
          </div>

          <kadr-input
            [label]="'settings.company.name' | transloco"
            [value]="name()"
            [error]="nameError()"
            [disabled]="!canUpdate()"
            (valueChange)="name.set($event)"
          />

          @if (formError()) {
            <p class="text-sm text-danger" role="alert">{{ formError() }}</p>
          }
          @if (saveSuccess()) {
            <p class="text-sm text-success" role="status">{{ 'settings.company.saved' | transloco }}</p>
          }

          <div class="flex justify-end">
            <kadr-button
              variant="primary"
              type="button"
              [disabled]="!canUpdate()"
              [loading]="saving()"
              (click)="save()"
            >
              {{ 'settings.company.save' | transloco }}
            </kadr-button>
          </div>
        </section>
      </div>
    }
  `,
})
export class CompanySettingsPage implements OnInit {
  private readonly tenantApi = inject(TenantApi);
  private readonly auth = inject(AuthService);
  private readonly transloco = inject(TranslocoService);

  protected readonly state = signal<PageState>('loading');
  protected readonly name = signal('');
  protected readonly logoUrl = signal<string | null>(null);
  protected readonly logoVersion = signal(0);
  protected readonly nameError = signal<string | undefined>(undefined);
  protected readonly formError = signal<string | null>(null);
  protected readonly saveSuccess = signal(false);
  protected readonly saving = signal(false);
  protected readonly logoBusy = signal(false);

  protected readonly canUpdate = computed(() =>
    this.auth.hasPermission(PermissionCodes.TenantSettingsUpdate),
  );

  protected readonly showTemplates = computed(() =>
    this.auth.hasPermission(PermissionCodes.TenantTemplatesManage),
  );

  ngOnInit(): void {
    void this.reload();
  }

  reload(): void {
    void this.load();
  }

  async save(): Promise<void> {
    this.saveSuccess.set(false);
    this.formError.set(null);
    const trimmed = this.name().trim();
    if (!trimmed) {
      this.nameError.set(this.transloco.translate('settings.company.nameRequired'));
      return;
    }
    this.nameError.set(undefined);
    this.saving.set(true);
    try {
      const result = await this.tenantApi.updateSettings({ name: trimmed });
      this.name.set(result.name);
      this.setLogoUrl(result.logoUrl);
      await this.auth.refreshMe();
      this.saveSuccess.set(true);
    } catch (error) {
      this.formError.set(this.mapError(error, 'settings.company.saveFailed'));
    } finally {
      this.saving.set(false);
    }
  }

  async onLogoSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file || !this.canUpdate()) {
      return;
    }
    this.logoBusy.set(true);
    this.formError.set(null);
    this.saveSuccess.set(false);
    try {
      const result = await this.tenantApi.uploadLogo(file);
      this.setLogoUrl(result.logoUrl);
      await this.auth.refreshMe();
    } catch (error) {
      this.formError.set(this.mapError(error, 'settings.company.logoFailed'));
    } finally {
      this.logoBusy.set(false);
    }
  }

  async removeLogo(): Promise<void> {
    if (!this.canUpdate()) {
      return;
    }
    this.logoBusy.set(true);
    this.formError.set(null);
    try {
      await this.tenantApi.deleteLogo();
      this.setLogoUrl(null);
      await this.auth.refreshMe();
    } catch (error) {
      this.formError.set(this.mapError(error, 'settings.company.logoFailed'));
    } finally {
      this.logoBusy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.state.set('loading');
    this.formError.set(null);
    try {
      const settings = await this.tenantApi.getSettings();
      this.name.set(settings.name);
      this.setLogoUrl(settings.logoUrl);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  private setLogoUrl(url: string | null): void {
    this.logoUrl.set(url);
    this.logoVersion.update((v) => v + 1);
  }

  private mapError(error: unknown, fallbackKey: string): string {
    const problem = problemDetailsFromError(error);
    switch (problem?.code) {
      case 'file.too_large':
        return this.transloco.translate('settings.company.errors.tooLarge');
      case 'file.invalid_type':
        return this.transloco.translate('settings.company.errors.invalidType');
      default:
        return problem?.detail || this.transloco.translate(fallbackKey);
    }
  }
}
