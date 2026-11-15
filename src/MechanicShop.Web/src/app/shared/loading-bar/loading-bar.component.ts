import { ChangeDetectionStrategy, Component } from '@angular/core';

import { loadingSignal } from '../../core/loading';

/**
 * Top loading bar shown while HTTP requests are pending.
 */
@Component({
  selector: 'app-loading-bar',
  standalone: true,
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <div class="loading-bar">
        <div class="loading-bar__fill"></div>
      </div>
    }
  `,
  styles: [
    `
      :host {
        display: block;
        position: fixed;
        top: 0;
        left: 0;
        right: 0;
        height: 3px;
        z-index: 10000;
      }

      .loading-bar {
        position: relative;
        width: 100%;
        height: 3px;
        background: rgba(255, 107, 44, 0.12);
        overflow: hidden;
      }

      .loading-bar__fill {
        position: absolute;
        top: 0;
        left: 0;
        height: 100%;
        width: 0%;
        background: linear-gradient(90deg, #ff6b2c, #6366f1);
        transition: width 300ms ease;
        box-shadow: 0 0 10px #ff6b2c, 0 0 20px #6366f1;
        animation: loadingShimmer 2s infinite;
      }

      @keyframes loadingShimmer {
        0% {
          left: -100%;
          width: 30%;
        }
        60% {
          width: 70%;
        }
        100% {
          left: 100%;
          width: 30%;
        }
      }
    `,
  ],
})
export class LoadingBarComponent {
  protected readonly loading = loadingSignal;
}
