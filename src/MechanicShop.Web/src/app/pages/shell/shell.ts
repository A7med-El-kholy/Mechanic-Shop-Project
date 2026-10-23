import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthStore } from '../../core/auth-store';

/** Staff navigation, mirroring the prototype's manager tab list. "Soon" tabs
 * arrive with later roadmap stages and stay visibly disabled meanwhile. */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class ShellPage {
  private readonly router = inject(Router);

  protected readonly auth = inject(AuthStore);

  protected readonly liveNav = [
    { path: 'dashboard', label: 'Dashboard', icon: '▦' },
    { path: 'customers', label: 'Customers', icon: '☰' },
    { path: 'vehicles', label: 'Vehicles', icon: '⌂' },
    { path: 'work-orders', label: 'Work Orders', icon: '≡' },
    { path: 'appointments', label: 'Appointments', icon: '◷' },
    { path: 'technicians', label: 'Technicians', icon: '⚒' },
    { path: 'invoices', label: 'Invoices', icon: '$' },
    { path: 'inventory', label: 'Inventory', icon: '⬡' },
    { path: 'reports', label: 'Reports', icon: 'Σ' },
  ];

  protected readonly plannedNav = [
    { label: 'Notifications' },
  ];

  protected readonly initials = computed(() => {
    const name = this.auth.fullName().trim();

    return name
      .split(/\s+/)
      .slice(0, 2)
      .map(part => part[0]?.toUpperCase() ?? '')
      .join('');
  });

  protected logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
