import { Injectable, inject, signal } from '@angular/core';

export interface Toast {
  id: number;
  type: 'success' | 'error' | 'warn' | 'info';
  message: string;
  duration?: number;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly toasts = signal<Toast[]>([]);
  readonly all = this.toasts.asReadonly();

  private id = 0;

  show(message: string, type: Toast['type'] = 'info', duration = 4000): void {
    const id = ++this.id;
    const toast: Toast = { id, type, message, duration };
    this.toasts.update((t) => [...t, toast]);

    if (duration > 0) {
      setTimeout(() => this.remove(id), duration);
    }
  }

  success(message: string, duration?: number): void {
    this.show(message, 'success', duration);
  }

  error(message: string, duration?: number): void {
    this.show(message, 'error', duration);
  }

  warn(message: string, duration?: number): void {
    this.show(message, 'warn', duration);
  }

  info(message: string, duration?: number): void {
    this.show(message, 'info', duration);
  }

  remove(id: number): void {
    this.toasts.update((t) => t.filter((x) => x.id !== id));
  }

  clear(): void {
    this.toasts.set([]);
  }
}
