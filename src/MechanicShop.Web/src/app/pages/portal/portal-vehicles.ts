import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';

import { Customer } from '../../core/types';
import { problemMessage } from '../../core/api';

/** The customer's garage as the workshop keeps it: live cars only — archived
 * ones leave this list by design (they exist once they have history). */
@Component({
  selector: 'app-portal-vehicles',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './portal-vehicles.html',
  styleUrl: './portal-vehicles.scss',
})
export class PortalVehiclesPage {
  private readonly http = inject(HttpClient);

  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);
  protected readonly profile = signal<Customer | null>(null);

  ngOnInit(): void {
    this.http
      .get<Customer>('/api/customers/me')
      .subscribe({
        next: profile => {
          this.profile.set(profile);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.pageError.set(problemMessage(error));
          this.loading.set(false);
        },
      });
  }
}
