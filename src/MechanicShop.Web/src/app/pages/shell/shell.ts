import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import {
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';

import { AuthStore } from '../../core/auth-store';
import { NotificationBadgeService } from '../../core/notifications';

/** Staff shell. The nav is the role's map: managers see the whole shop, technicians
 * see their four destinations (the prototype's T tree — same pages, "My" labels). */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class ShellPage {
  private readonly router = inject(Router);

  protected readonly auth = inject(AuthStore);
  protected readonly badge = inject(NotificationBadgeService);

  protected readonly managerNav = [
    { path: 'dashboard', label: 'Dashboard', icon: '▦' },
    { path: 'ai-assistant', label: 'AI Diagnostics', icon: '🤖' },
    { path: 'customers', label: 'Customers', icon: '☰' },
    { path: 'vehicles', label: 'Vehicles', icon: '⌂' },
    { path: 'work-orders', label: 'Work Orders', icon: '≡' },
    { path: 'appointments', label: 'Appointments', icon: '◷' },
    { path: 'technicians', label: 'Technicians', icon: '⚒' },
    { path: 'invoices', label: 'Invoices', icon: '$' },
    { path: 'inventory', label: 'Inventory', icon: '⬡' },
    { path: 'reports', label: 'Reports', icon: 'Σ' },
    { path: 'notifications', label: 'Notifications', icon: '◔' },
  ];

  protected readonly technicianNav = [
    { path: 'tech-home', label: 'My Dashboard', icon: '▦' },
    { path: 'ai-assistant', label: 'AI Diagnostics', icon: '🤖' },
    { path: 'work-orders', label: 'My Work Orders', icon: '≡' },
    { path: 'appointments', label: 'My Schedule', icon: '◷' },
    { path: 'notifications', label: 'Notifications', icon: '◔' },
  ];

  protected readonly nav = computed(() =>
    this.auth.isManager() ? this.managerNav : this.technicianNav);

  protected readonly currentPath = () =>
    this.router.url.split('/').pop() ?? '';

  protected readonly initials = computed(() => {
    const name = this.auth.fullName().trim();

    return name
      .split(/\s+/)
      .slice(0, 2)
      .map(part => part[0]?.toUpperCase() ?? '')
      .join('');
  });

  ngOnInit(): void {
    this.badge.refresh('staff');
  }

  protected logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
