import { Directive, ElementRef, effect, inject, input } from '@angular/core';

@Directive({ selector: '[appCountUp]' })
export class CountUpDirective {
  readonly value = input<number | null | undefined>(0, { alias: 'appCountUp' });
  readonly decimals = input(0);
  readonly duration = input(900);
  readonly prefix = input('');
  readonly suffix = input('');

  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private current = 0;
  private frame = 0;

  constructor() {
    effect((onCleanup) => {
      const target = Number(this.value() ?? 0);
      const decimals = this.decimals();
      const prefix = this.prefix();
      const suffix = this.suffix();
      const render = (n: number) => {
        this.el.textContent = `${prefix}${n.toLocaleString(undefined, {
          minimumFractionDigits: decimals,
          maximumFractionDigits: decimals,
        })}${suffix}`;
      };

      const reduce = globalThis.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
      if (reduce || !Number.isFinite(target) || target === this.current) {
        this.current = Number.isFinite(target) ? target : 0;
        render(this.current);
        return;
      }

      const from = this.current;
      const start = performance.now();
      const duration = this.duration();

      const tick = (now: number) => {
        const t = Math.min(1, (now - start) / duration);
        const eased = 1 - Math.pow(1 - t, 4);
        this.current = from + (target - from) * eased;
        render(this.current);
        if (t < 1) {
          this.frame = requestAnimationFrame(tick);
        } else {
          this.current = target;
          render(target);
        }
      };

      this.frame = requestAnimationFrame(tick);
      onCleanup(() => cancelAnimationFrame(this.frame));
    });
  }
}
