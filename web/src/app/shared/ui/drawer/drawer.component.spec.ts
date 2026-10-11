import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DrawerComponent } from './drawer.component';

@Component({
  selector: 'kadr-drawer-host',
  imports: [DrawerComponent],
  template: `
    <kadr-drawer
      [open]="open"
      title="عنوان الدرج"
      closeLabel="إغلاق"
      (closed)="onClosed()"
    >
      <p>محتوى</p>
    </kadr-drawer>
  `,
})
class DrawerHostComponent {
  open = true;
  onClosed = vi.fn();
}

describe('DrawerComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DrawerHostComponent],
    }).compileComponents();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders as bottom sheet below 640px', async () => {
    vi.stubGlobal(
      'matchMedia',
      vi.fn().mockImplementation((query: string) => ({
        matches: query.includes('max-width: 639px'),
        media: query,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        addListener: vi.fn(),
        removeListener: vi.fn(),
        dispatchEvent: vi.fn(),
        onchange: null,
      })),
    );

    const fixture = TestBed.createComponent(DrawerHostComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const root = fixture.nativeElement.querySelector('[data-testid="drawer-root"]') as HTMLElement;
    expect(root.getAttribute('data-mode')).toBe('sheet');
    expect(root.querySelector('.kadr-drawer-sheet')).toBeTruthy();
  });

  it('renders as side panel at 640px and above', async () => {
    vi.stubGlobal(
      'matchMedia',
      vi.fn().mockImplementation((query: string) => ({
        matches: false,
        media: query,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        addListener: vi.fn(),
        removeListener: vi.fn(),
        dispatchEvent: vi.fn(),
        onchange: null,
      })),
    );

    const fixture = TestBed.createComponent(DrawerHostComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    const root = fixture.nativeElement.querySelector('[data-testid="drawer-root"]') as HTMLElement;
    expect(root.getAttribute('data-mode')).toBe('panel');
    expect(root.querySelector('.kadr-drawer-sheet')).toBeFalsy();
  });

  it('emits closed on Escape', async () => {
    vi.stubGlobal(
      'matchMedia',
      vi.fn().mockImplementation(() => ({
        matches: true,
        media: '',
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        addListener: vi.fn(),
        removeListener: vi.fn(),
        dispatchEvent: vi.fn(),
        onchange: null,
      })),
    );

    const fixture = TestBed.createComponent(DrawerHostComponent);
    fixture.detectChanges();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(fixture.componentInstance.onClosed).toHaveBeenCalled();
  });
});
