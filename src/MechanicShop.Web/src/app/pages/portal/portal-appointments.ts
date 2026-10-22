import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

import { AppointmentRequestRow, Customer, ServiceOption } from '../../core/types';
import { fmtDate } from '../../core/format';
import { problemMessage } from '../../core/api';

/** "Appointments" for the customer: describe WHAT (service or "not sure") and
 * WHEN (a preferred date). A request is NOT a booking — the workshop reads the
 * pending list and confirms a real slot; the panel is honest about that. */
@Component({
  selector: 'app-portal-appointments',
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './portal-appointments.html',
  styleUrl: './portal-appointments.scss',
})
export class PortalAppointmentsPage {
  private readonly http = inject(HttpClient);

  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);

  protected readonly vehicles = signal<Customer['vehicles']>([]);
  protected readonly services = signal<ServiceOption[]>([]);
  protected readonly requests = signal<AppointmentRequestRow[]>([]);

  // The form. `Not sure — inspection` is an EMPTY service value.
  protected readonly vehicleId = signal('');
  protected readonly serviceTaskId = signal('');
  protected readonly preferredDate = signal('');
  protected readonly today = new Date().toISOString().slice(0, 10);

  protected readonly busy = signal(false);
  protected readonly alert = signal<string | null>(null);
  protected readonly alertKind = signal<'ok' | 'err'>('ok');

  protected readonly fmtDate = fmtDate;

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.pageError.set(null);

    this.http
      .get<Customer>('/api/customers/me')
      .subscribe({
        next: profile => {
          this.vehicles.set(profile.vehicles);
          this.loadServices();
        },
        error: (error: HttpErrorResponse) => {
          this.pageError.set(problemMessage(error));
          this.loading.set(false);
        },
      });
  }

  private loadServices(): void {
    this.http
      .get<ServiceOption[]>('/api/portal/service-options')
      .subscribe({
        next: services => {
          this.services.set(services);
          this.loadRequests();
        },
        error: (error: HttpErrorResponse) => {
          this.pageError.set(problemMessage(error));
          this.loading.set(false);
        },
      });
  }

  private loadRequests(): void {
    this.http
      .get<AppointmentRequestRow[]>('/api/appointment-requests/mine')
      .subscribe({
        next: requests => {
          this.requests.set(requests);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.pageError.set(problemMessage(error));
          this.loading.set(false);
        },
      });
  }

  protected submit(): void {
    this.alert.set(null);
    this.busy.set(true);

    const vehicleId = this.vehicleId();
    const serviceTaskId = this.serviceTaskId();
    const preferredDate = this.preferredDate();

    this.http
      .post('/api/appointment-requests', {
        vehicleId,
        serviceTaskId: serviceTaskId === '' ? null : serviceTaskId,
        preferredDate,
      })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.alertKind.set('ok');
          this.alert.set('Request sent — the workshop will confirm a slot shortly.');
          this.loadRequests();
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.alertKind.set('err');
          this.alert.set(problemMessage(error) ?? 'The request could not be sent.');
        },
      });
  }
}
