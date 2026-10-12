import { OverlayModule } from '@angular/cdk/overlay';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { provideTestTransloco } from '../../../testing/transloco-testing';
import { ComboboxComponent, ComboboxOption } from './combobox.component';

@Component({
  selector: 'kadr-combobox-host',
  imports: [ComboboxComponent],
  template: `
    <kadr-combobox
      label="Assignee"
      [searchFn]="search"
      [value]="value"
      [displayName]="displayName"
      (selectionChange)="onSelect($event)"
    />
  `,
})
class ComboboxHostComponent {
  value = '';
  displayName = '';
  selected: ComboboxOption | null = null;
  search = vi.fn(async (q: string) => {
    const all = [
      { id: 'a1', name: 'أحمد' },
      { id: 'b2', name: 'Branch North' },
    ];
    const term = q.trim().toLowerCase();
    return all.filter((x) => !term || x.name.toLowerCase().includes(term));
  });
  onSelect(opt: ComboboxOption | null): void {
    this.selected = opt;
    this.value = opt?.id ?? '';
    this.displayName = opt?.name ?? '';
  }
}

describe('ComboboxComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ComboboxHostComponent, OverlayModule],
      providers: [provideTestTransloco()],
    }).compileComponents();
  });

  it('selects an option with keyboard Enter', async () => {
    const fixture = TestBed.createComponent(ComboboxHostComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const input = fixture.nativeElement.querySelector('input[role="combobox"]') as HTMLInputElement;
    input.focus();
    input.dispatchEvent(new Event('focus'));
    fixture.detectChanges();

    // Allow debounce + search
    await new Promise((r) => setTimeout(r, 300));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    fixture.detectChanges();
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.componentInstance.selected?.id).toBeTruthy();
    expect(fixture.componentInstance.displayName.length).toBeGreaterThan(0);
    expect(fixture.componentInstance.search).toHaveBeenCalled();
  });

  it('closes on Escape', async () => {
    const fixture = TestBed.createComponent(ComboboxHostComponent);
    fixture.detectChanges();
    const input = fixture.nativeElement.querySelector('input[role="combobox"]') as HTMLInputElement;
    input.dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    await new Promise((r) => setTimeout(r, 300));
    fixture.detectChanges();

    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    const list = document.querySelector('[role="listbox"]');
    expect(list).toBeFalsy();
  });

  it('re-searches on reopen after ready so scope switches never keep stale options', async () => {
    const fixture = TestBed.createComponent(ComboboxHostComponent);
    const host = fixture.componentInstance;
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input[role="combobox"]') as HTMLInputElement;
    input.dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    await new Promise((r) => setTimeout(r, 300));
    fixture.detectChanges();
    expect(host.search).toHaveBeenCalledTimes(1);

    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    // Simulate AssignmentTargetPicker swapping searchFn (new identity).
    host.search = vi.fn(async () => [{ id: 'd1', name: 'قسم المبيعات' }]);
    fixture.detectChanges();
    await fixture.whenStable();

    input.dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    await new Promise((r) => setTimeout(r, 300));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(host.search).toHaveBeenCalled();
    const options = Array.from(document.querySelectorAll('[role="option"]')).map((el) =>
      (el.textContent ?? '').trim(),
    );
    expect(options).toEqual(['قسم المبيعات']);
    expect(options).not.toContain('أحمد');
  });
});
