import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { KadrRevealDirective } from './kadr-reveal.directive';

@Component({
  standalone: true,
  imports: [KadrRevealDirective],
  template: `<div kadrReveal data-testid="reveal">Hi</div>`,
})
class HostComponent {}

describe('KadrRevealDirective', () => {
  beforeEach(() => {
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      configurable: true,
      value: (query: string) =>
        ({
          matches: query.includes('prefers-reduced-motion: reduce'),
          media: query,
          onchange: null,
          addEventListener: vi.fn(),
          removeEventListener: vi.fn(),
          addListener: vi.fn(),
          removeListener: vi.fn(),
          dispatchEvent: vi.fn(),
        }) as unknown as MediaQueryList,
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('skips transform when prefers-reduced-motion', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('[data-testid="reveal"]') as HTMLElement;
    expect(el.style.opacity).toBe('1');
    expect(el.style.transform).toBe('none');
  });
});
