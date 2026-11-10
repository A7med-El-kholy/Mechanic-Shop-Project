import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';

import { AuthStore } from '../../core/auth-store';
import { NotificationBadgeService } from '../../core/notifications';

@Component({
  selector: 'app-portal-shell',
  imports: [RouterLink, RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './portal-shell.html',
  styleUrl: './portal-shell.scss',
})
export class PortalShellPage {
  private readonly router = inject(Router);

  protected readonly auth = inject(AuthStore);
  protected readonly badge = inject(NotificationBadgeService);

  protected readonly portalNav = [
    { path: 'portal', label: 'Dashboard', exact: true },
    { path: 'portal/vehicles', label: 'My Vehicles', exact: false },
    { path: 'portal/repairs', label: 'My Repairs', exact: false },
    { path: 'portal/invoices', label: 'Invoices', exact: false },
    { path: 'portal/shop', label: 'Parts Shop', exact: false },
    { path: 'portal/appointments', label: 'Appointments', exact: false },
    { path: 'portal/ai-assistant', label: 'AI Assistant', exact: false },
    { path: 'portal/notifications', label: 'Notifications', exact: false },
  ];

  protected readonly initials = computed(() => {
    const name = this.auth.fullName().trim();
    return name
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join('');
  });

  protected isActive(path: string): boolean {
    return this.router.isActive(path, { paths: 'exact', queryParams: 'ignored', fragment: 'ignored', matrixParams: 'ignored' }) ||
      (path !== 'portal' && this.router.url.startsWith(path + '/'));
  }

  ngOnInit(): void {
    this.badge.refresh('portal');
  }

  protected logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
