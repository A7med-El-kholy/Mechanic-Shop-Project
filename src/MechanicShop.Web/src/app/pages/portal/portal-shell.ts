import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthStore } from '../../core/auth-store';

/** The customer-facing side. Lighter than the staff shell by design — customers
 * watch their cars and repairs and ask the workshop for appointments; they do
 * not manage anything. */
@Component({
  selector: 'app-portal-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './portal-shell.html',
  styleUrl: './portal-shell.scss',
})
export class PortalShellPage {
  private readonly router = inject(Router);

  protected readonly auth = inject(AuthStore);

  protected readonly portalNav = [
    { path: 'portal', label: 'Home', exact: true },
    { path: 'portal/vehicles', label: 'My Vehicles', exact: false },
    { path: 'portal/repairs', label: 'My Repairs', exact: false },
    { path: 'portal/invoices', label: 'Invoices', exact: false },
    { path: 'portal/shop', label: 'Parts Shop', exact: false },
    { path: 'portal/appointments', label: 'Appointments', exact: false },
  ];

  protected readonly plannedNav = ['AI Assistant', 'Notifications'];

  protected readonly firstName = computed(() => {
    const name = this.auth.fullName().trim();

    return name.split(/\s+/)[0] || name;
  });

  protected logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
