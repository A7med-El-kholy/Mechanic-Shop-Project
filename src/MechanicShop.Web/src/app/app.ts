import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import {
  NavigationCancel,
  NavigationEnd,
  NavigationError,
  NavigationStart,
  Router,
  RouterOutlet,
} from '@angular/router';

import { MotionService } from './core/motion';
import { LoadingBarComponent } from './shared/loading-bar/loading-bar.component';
import { ToastComponent } from './shared/toast/toast.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, LoadingBarComponent, ToastComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="navBar" [class.navBar--active]="navigating()" aria-hidden="true"></div>
    <app-loading-bar />
    <app-toast />
    <router-outlet />
  `,
})
export class App implements OnInit {
  private readonly router = inject(Router);
  private readonly motion = inject(MotionService);

  protected readonly navigating = signal(false);

  ngOnInit(): void {
    this.motion.start();

    this.router.events.subscribe((event) => {
      if (event instanceof NavigationStart) {
        this.navigating.set(true);
      } else if (
        event instanceof NavigationEnd ||
        event instanceof NavigationCancel ||
        event instanceof NavigationError
      ) {
        this.navigating.set(false);
      }
    });
  }
}
