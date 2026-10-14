import { DOCUMENT } from '@angular/common';
import {
  Directive,
  ElementRef,
  OnDestroy,
  OnInit,
  inject,
  input,
} from '@angular/core';

@Directive({
  selector: '[kadrReveal]',
  standalone: true,
})
export class KadrRevealDirective implements OnInit, OnDestroy {
  private readonly el = inject(ElementRef<HTMLElement>);
  private readonly document = inject(DOCUMENT);
  private observer: IntersectionObserver | null = null;

  /** Stagger delay in ms within a group. */
  readonly kadrRevealDelay = input(0);

  ngOnInit(): void {
    const node = this.el.nativeElement;
    const reduce =
      typeof matchMedia === 'function' &&
      matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (reduce) {
      node.style.opacity = '1';
      node.style.transform = 'none';
      return;
    }

    node.style.opacity = '0';
    node.style.transform = 'translateY(1rem)';
    node.style.transitionProperty = 'opacity, transform';
    node.style.transitionDuration = 'var(--dur-reveal)';
    node.style.transitionTimingFunction = 'var(--ease)';
    if (this.kadrRevealDelay() > 0) {
      node.style.transitionDelay = `${this.kadrRevealDelay()}ms`;
    }

    const IO = this.document.defaultView?.IntersectionObserver;
    if (!IO) {
      node.style.opacity = '1';
      node.style.transform = 'none';
      return;
    }

    this.observer = new IO(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            node.style.opacity = '1';
            node.style.transform = 'translateY(0)';
            this.observer?.unobserve(node);
          }
        }
      },
      { root: null, rootMargin: '0px 0px -8% 0px', threshold: 0.12 },
    );
    this.observer.observe(node);
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }
}
