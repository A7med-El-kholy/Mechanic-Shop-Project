import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class MotionService {
  private readonly doc = inject(DOCUMENT);
  private started = false;
  private observer?: IntersectionObserver;

  private static readonly REVEAL_SELECTOR = [
    '.pageHeader',
    '.card',
    '.statCard',
    '.feature',
    '.serviceCard',
    '.testimonial',
    '.trustItem',
    '.aiStep',
    '.stat',
    '.ctaStrip',
    'table.plain tbody tr',
  ].join(',');

  private static readonly SPOTLIGHT_SELECTOR = '.card, .statCard, .feature, .serviceCard, .btn';

  start(): void {
    const win = this.doc.defaultView;
    if (this.started || !win) {
      return;
    }
    this.started = true;

    if (win.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      return;
    }

    this.observer = new IntersectionObserver((entries) => this.onIntersect(entries), {
      threshold: 0.06,
      rootMargin: '0px 0px -5% 0px',
    });

    this.scan(this.doc.body);

    new MutationObserver((mutations) => {
      for (const mutation of mutations) {
        mutation.addedNodes.forEach((node) => {
          if (node.nodeType === Node.ELEMENT_NODE) {
            this.scan(node as Element);
          }
        });
      }
    }).observe(this.doc.body, { childList: true, subtree: true });

    this.doc.addEventListener(
      'pointermove',
      (event) => {
        const target = (event.target as Element | null)?.closest?.(MotionService.SPOTLIGHT_SELECTOR);
        if (target instanceof HTMLElement) {
          const rect = target.getBoundingClientRect();
          target.style.setProperty('--mx', `${event.clientX - rect.left}px`);
          target.style.setProperty('--my', `${event.clientY - rect.top}px`);
        }
      },
      { passive: true },
    );
  }

  private scan(root: Element): void {
    const found: Element[] = [];
    if (root.matches(MotionService.REVEAL_SELECTOR)) {
      found.push(root);
    }
    root.querySelectorAll(MotionService.REVEAL_SELECTOR).forEach((el) => found.push(el));

    for (const el of found) {
      if (!(el instanceof HTMLElement) || el.dataset['motion']) {
        continue;
      }
      el.dataset['motion'] = '1';

      const siblings = el.parentElement ? Array.from(el.parentElement.children) : [];
      const index = Math.max(0, siblings.indexOf(el));
      el.style.setProperty('--reveal-delay', `${Math.min(index, 9) * 65}ms`);
      el.classList.add('reveal');
      this.observer?.observe(el);
    }
  }

  private onIntersect(entries: IntersectionObserverEntry[]): void {
    for (const entry of entries) {
      if (!entry.isIntersecting || !(entry.target instanceof HTMLElement)) {
        continue;
      }
      const el = entry.target;
      this.observer?.unobserve(el);
      el.classList.add('reveal--in');

      setTimeout(() => {
        el.classList.remove('reveal', 'reveal--in');
        el.style.removeProperty('--reveal-delay');
      }, 1100);
    }
  }
}
