import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';

import { Customer } from '../../core/types';
import { VehicleForm } from '../../shared/vehicle-form';
import { VehiclePayload } from '../../shared/vehicle-fields';
import { problemMessage } from '../../core/api';

/** One customer's profile and their ACTIVE vehicles. The detail response comes
 * from the API already filtered (archived cars excluded), so this screen is the
 * customer's live garage; cars with no history delete, cars with any work-order
 * history get archived instead — the server enforces both and speaks through
 * ProblemDetails. */
@Component({
  selector: 'app-customer-detail',
  imports: [RouterLink, VehicleForm],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './customer-detail.html',
  styleUrl: './customer-detail.scss',
})
export class CustomerDetailPage {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly customerId = input.required<string>();

  protected readonly customer = signal<Customer | null>(null);
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);
  protected readonly alert = signal<string | null>(null);

  protected readonly vehicleDialog = signal<'closed' | 'new' | 'edit'>('closed');
  protected readonly editingVehicle = signal<VehiclePayload | null>(null);
  protected readonly editingVehicleId = signal<string | null>(null);
  protected readonly vehicleBusy = signal(false);
  protected readonly vehicleError = signal<string | null>(null);

  constructor() {
    // The input comes from the route param; re-load whenever it changes.
    effect(() => {
      this.customerId();
      this.load();
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.pageError.set(null);

    this.http
      .get<Customer>(`/api/customers/${this.customerId()}`)
      .subscribe({
        next: customer => {
          this.customer.set(customer);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.pageError.set(problemMessage(error));
          this.loading.set(false);
        },
      });
  }

  protected removeCustomer(): void {
    const customer = this.customer();

    if (!customer) {
      return;
    }

    if (!window.confirm(`Delete "${customer.name}"? Their vehicles without history go with them.`)) {
      return;
    }

    this.http.delete(`/api/customers/${customer.customerId}`).subscribe({
      next: () => void this.router.navigate(['/customers']),
      error: (error: HttpErrorResponse) => this.alert.set(problemMessage(error)),
    });
  }

  protected openNewVehicle(): void {
    this.editingVehicle.set(null);
    this.editingVehicleId.set(null);
    this.vehicleError.set(null);
    this.vehicleDialog.set('new');
  }

  protected openEditVehicle(vehicle: {
    vehicleId: string;
    make: string;
    model: string;
    year: number;
    licensePlate: string;
    vin: string;
  }): void {
    this.editingVehicle.set({
      make: vehicle.make,
      model: vehicle.model,
      year: vehicle.year,
      licensePlate: vehicle.licensePlate,
      vin: vehicle.vin,
    });
    this.editingVehicleId.set(vehicle.vehicleId);
    this.vehicleError.set(null);
    this.vehicleDialog.set('edit');
  }

  protected closeVehicleDialog(): void {
    this.vehicleDialog.set('closed');
  }

  protected saveVehicle(payload: VehiclePayload): void {
    const mode = this.vehicleDialog();
    const customer = this.customer();

    if (mode === 'closed' || !customer) {
      return;
    }

    this.vehicleBusy.set(true);
    this.vehicleError.set(null);

    const request$ =
      mode === 'new'
        ? this.http.post(`/api/customers/${customer.customerId}/vehicles`, payload)
        : this.http.put(`/api/vehicles/${this.editingVehicleId()}`, payload);

    request$.subscribe({
      next: () => {
        this.vehicleBusy.set(false);
        this.vehicleDialog.set('closed');
        this.alert.set(null);
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.vehicleBusy.set(false);
        this.vehicleError.set(problemMessage(error));
      },
    });
  }

  protected removeVehicle(vehicle: { vehicleId: string; licensePlate: string }): void {
    if (!window.confirm(`Delete "${vehicle.licensePlate}"?`)) {
      return;
    }

    this.http.delete(`/api/vehicles/${vehicle.vehicleId}`).subscribe({
      next: () => {
        this.alert.set('Vehicle removed. Cars with service history get archived instead.');
        this.load();
      },
      error: (error: HttpErrorResponse) => this.alert.set(problemMessage(error)),
    });
  }
}
