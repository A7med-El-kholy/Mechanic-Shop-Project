import { HttpClient } from '@angular/common/http';
import { Injectable, OnDestroy, inject, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ApiHealthService implements OnDestroy {
  private readonly http = inject(HttpClient);
  private timer?: ReturnType<typeof setInterval>;

  readonly online = signal<boolean | null>(null);

  start(): void {
    if (this.timer) {
      return;
    }
    this.check();
    this.timer = setInterval(() => this.check(), 30_000);
  }

  private check(): void {
    this.http.get('/healthz', { responseType: 'text' }).subscribe({
      next: () => this.online.set(true),
      error: () => this.online.set(false),
    });
  }

  ngOnDestroy(): void {
    clearInterval(this.timer);
  }
}
