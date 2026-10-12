import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import {
  SalaryImportApi,
  SalaryImportCommitRow,
  SalaryImportPreviewResult,
} from '../../core/api/salary-import.api';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

type Step = 1 | 2 | 3 | 4;

@Component({
  selector: 'kadr-salaries-import-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslocoPipe, ButtonComponent, EmptyStateComponent],
  template: `
    <div class="mx-auto flex w-full max-w-3xl flex-col gap-5">
      <nav class="text-sm text-fg-2">
        <a routerLink="/employees" class="text-accent hover:underline">{{
          'employees.title' | transloco
        }}</a>
        <span class="mx-1">/</span>
        <span>{{ 'employees.salary.importTitle' | transloco }}</span>
      </nav>

      <header class="text-start">
        <h1 class="text-2xl font-bold text-fg">{{ 'employees.salary.importTitle' | transloco }}</h1>
        <p class="mt-1 text-sm text-fg-2">{{ 'employees.salary.importSubtitle' | transloco }}</p>
      </header>

      <ol
        class="flex flex-wrap gap-2 text-xs"
        [attr.aria-label]="'employees.salary.importStepsAria' | transloco"
      >
        @for (s of stepMeta; track s.id) {
          <li
            class="rounded-full px-3 py-1"
            [class.bg-accent]="step() === s.id"
            [class.text-on-accent]="step() === s.id"
            [class.bg-subtle]="step() !== s.id"
            [class.text-fg-2]="step() !== s.id"
          >
            {{ s.labelKey | transloco }}
          </li>
        }
      </ol>

      @if (step() === 1) {
        <section class="rounded-lg border border-line bg-surface p-4 text-start">
          <p class="text-sm text-fg-2">{{ 'employees.salary.importUploadHint' | transloco }}</p>
          <div class="mt-4 flex flex-wrap gap-2">
            <kadr-button
              variant="secondary"
              type="button"
              [loading]="downloading()"
              (click)="downloadTemplate()"
            >
              {{ 'employees.downloadTemplate' | transloco }}
            </kadr-button>
          </div>
          <label class="mt-4 grid gap-2 text-sm font-medium text-fg">
            <span>{{ 'employees.chooseFile' | transloco }}</span>
            <input type="file" accept=".csv,text/csv" (change)="onFile($event)" />
          </label>
          @if (fileError()) {
            <p class="mt-2 text-sm text-danger" role="alert">{{ fileError() }}</p>
          }
          <div class="mt-4 flex justify-end">
            <kadr-button
              variant="primary"
              type="button"
              [disabled]="!file()"
              [loading]="previewing()"
              (click)="preview()"
            >
              {{ 'employees.continue' | transloco }}
            </kadr-button>
          </div>
        </section>
      } @else if (step() === 2) {
        <section class="rounded-lg border border-line bg-surface p-4 text-start">
          <p class="text-sm text-fg-2">{{ 'employees.salary.importMapHint' | transloco }}</p>
          <ul class="mt-3 list-inside list-disc text-sm text-fg">
            <li>employeeNumber, effectiveFrom, basicAmount, changeReason</li>
            <li>currency (optional, default EGP)</li>
            <li>{{ 'employees.salary.importMapComponents' | transloco }}</li>
          </ul>
          <div class="mt-4 flex justify-end gap-2">
            <kadr-button variant="secondary" type="button" (click)="step.set(1)">{{
              'onboarding.back' | transloco
            }}</kadr-button>
            <kadr-button variant="primary" type="button" (click)="step.set(3)">{{
              'employees.continue' | transloco
            }}</kadr-button>
          </div>
        </section>
      } @else if (step() === 3 && previewResult(); as p) {
        <section class="rounded-lg border border-line bg-surface p-4 text-start">
          <p class="text-sm">
            {{
              'employees.importReviewSummary'
                | transloco: { valid: p.validCount, errors: p.errorCount }
            }}
          </p>
          @if (p.errors.length) {
            <div class="mt-3 max-h-64 overflow-auto rounded-md border border-line">
              <table class="w-full text-start text-xs">
                <thead class="bg-subtle text-fg-2">
                  <tr>
                    <th class="px-2 py-2">{{ 'employees.importRow' | transloco }}</th>
                    <th class="px-2 py-2">{{ 'employees.importField' | transloco }}</th>
                    <th class="px-2 py-2">{{ 'employees.importMessage' | transloco }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (err of p.errors; track $index) {
                    <tr class="border-t border-line">
                      <td class="px-2 py-2 tabular-nums">{{ err.row }}</td>
                      <td class="px-2 py-2"><bdi>{{ err.field }}</bdi></td>
                      <td class="px-2 py-2">{{ err.message }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
          <div class="mt-4 flex flex-wrap justify-end gap-2">
            <kadr-button variant="secondary" type="button" (click)="step.set(1)">{{
              'onboarding.back' | transloco
            }}</kadr-button>
            <kadr-button
              variant="primary"
              type="button"
              [disabled]="p.validCount === 0"
              [loading]="committing()"
              (click)="commit()"
            >
              {{ 'employees.importValid' | transloco: { n: p.validCount } }}
            </kadr-button>
          </div>
        </section>
      } @else if (step() === 4) {
        <kadr-empty-state
          [title]="'employees.salary.importDoneTitle' | transloco"
          [description]="
            'employees.salary.importDoneBody'
              | transloco: { n: importedCount(), skipped: skippedCount() }
          "
        >
          <a routerLink="/employees" class="contents">
            <kadr-button variant="primary" type="button">{{
              'employees.backToList' | transloco
            }}</kadr-button>
          </a>
        </kadr-empty-state>
      }
    </div>
  `,
})
export class SalariesImportPage {
  private readonly api = inject(SalaryImportApi);
  private readonly i18n = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly stepMeta = [
    { id: 1 as Step, labelKey: 'employees.importStep1' },
    { id: 2 as Step, labelKey: 'employees.importStep2' },
    { id: 3 as Step, labelKey: 'employees.importStep3' },
    { id: 4 as Step, labelKey: 'employees.importStep4' },
  ];
  protected readonly step = signal<Step>(1);
  protected readonly file = signal<File | null>(null);
  protected readonly fileError = signal<string | null>(null);
  protected readonly downloading = signal(false);
  protected readonly previewing = signal(false);
  protected readonly committing = signal(false);
  protected readonly previewResult = signal<SalaryImportPreviewResult | null>(null);
  protected readonly validRows = signal<SalaryImportCommitRow[]>([]);
  protected readonly importedCount = signal(0);
  protected readonly skippedCount = signal(0);

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const f = input.files?.[0] ?? null;
    this.fileError.set(null);
    if (f && !f.name.toLowerCase().endsWith('.csv')) {
      this.fileError.set(this.i18n.translate('employees.importCsvOnly'));
      this.file.set(null);
      return;
    }
    this.file.set(f);
  }

  async downloadTemplate(): Promise<void> {
    this.downloading.set(true);
    try {
      const blob = await this.api.downloadTemplate();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'salary-import-template.csv';
      a.click();
      URL.revokeObjectURL(url);
    } catch {
      this.toast.show(this.i18n.translate('employees.salary.toastFail'), 'danger');
    } finally {
      this.downloading.set(false);
    }
  }

  async preview(): Promise<void> {
    const f = this.file();
    if (!f) return;
    this.previewing.set(true);
    this.fileError.set(null);
    try {
      const result = await this.api.preview(f);
      this.previewResult.set(result);
      this.validRows.set(result.validRows);
      this.step.set(2);
    } catch (err: unknown) {
      const code = (err as { error?: { code?: string } })?.error?.code;
      if (code === 'salary.import_invalid') {
        this.fileError.set(this.i18n.translate('employees.importCsvOnly'));
      } else {
        this.fileError.set(this.i18n.translate('employees.salary.toastFail'));
      }
    } finally {
      this.previewing.set(false);
    }
  }

  async commit(): Promise<void> {
    const rows = this.validRows();
    if (!rows.length) return;
    this.committing.set(true);
    try {
      const result = await this.api.commit(rows);
      this.importedCount.set(result.importedCount);
      this.skippedCount.set(result.skippedCount);
      this.step.set(4);
    } catch {
      this.toast.show(this.i18n.translate('employees.salary.toastFail'), 'danger');
    } finally {
      this.committing.set(false);
    }
  }
}
