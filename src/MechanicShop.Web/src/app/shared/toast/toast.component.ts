import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { ToastService, Toast } from '../../core/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (t of toastService.all(); track t.id) {
      <div
        class="toast"
        [class.toast--success]="t.type === 'success'"
        [class.toast--error]="t.type === 'error'"
        [class.toast--warn]="t.type === 'warn'"
        [class.toast--info]="t.type === 'info'"
        (click)="toastService.remove(t.id)"
      >
        <span class="toast__icon">{{ iconFor(t.type) }}</span>
        <span class="toast__message">{{ t.message }}</span>
        <button class="toast__close" (click)="toastService.remove(t.id); $event.stopPropagation()">✕</button>
      </div>
    }
  `,
  styles: [`
    :host {
      position: fixed;
      top: 1.5rem;
      right: 1.5rem;
      z-index: 10000;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      pointer-events: none;
    }

    .toast {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 1rem 1.25rem;
      border-radius: 12px;
      color: #fff;
      font-family: 'Inter', 'Outfit', sans-serif;
      font-size: 0.9rem;
      font-weight: 600;
      min-width: 300px;
      max-width: 450px;
      box-shadow: 0 10px 30px -5px rgba(0, 0, 0, 0.3);
      pointer-events: all;
      animation: toastSlide 300ms ease-out;
      border: 1px solid rgba(255, 255, 255, 0.1);
      backdrop-filter: blur(10px);
    }

    @keyframes toastSlide {
      from { transform: translateX(100%); opacity: 0; }
      to { transform: translateX(0); opacity: 1; }
    }

    .toast--success { background: rgba(34, 197, 94, 0.9); border-color: rgba(34, 197, 94, 0.3); }
    .toast--error { background: rgba(220, 38, 38, 0.9); border-color: rgba(220, 38, 38, 0.3); }
    .toast--warn { background: rgba(217, 119, 6, 0.94); border-color: rgba(217, 119, 6, 0.3); }
    .toast--info { background: rgba(99, 102, 241, 0.94); border-color: rgba(99, 102, 241, 0.3); }

    .toast__icon { font-size: 1.25rem; flex-shrink: 0; }
    .toast__message { flex: 1; }
    .toast__close {
      background: none;
      border: none;
      color: inherit;
      opacity: 0.7;
      font-size: 1.25rem;
      cursor: pointer;
      padding: 0;
      line-height: 1;
    }
    .toast__close:hover { opacity: 1; }
  `],
})
export class ToastComponent {
  protected readonly toastService = inject(ToastService);

  protected iconFor(type: Toast['type']): string {
    switch (type) {
      case 'success': return '✅';
      case 'error': return '❌';
      case 'warn': return '⚠️';
      default: return 'ℹ️';
    }
  }
}
