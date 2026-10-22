import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';

import { PaginatedList, VehicleListItem } from '../../core/types';
import { VehicleForm } from '../../shared/vehicle-form';
import { VehiclePayload } from '../../shared/vehicle-fields';
import { problemMessage } from '../../core/api';

const PAGE_SIZE = 10;

/** The whole garage as one searchable table. The default list answers "what can
 * the shop still call in?" (archived cars hidden); the toggle answers "what ever
 * arrived here?". Deletion keeps the history rule from the API: a car with any
 * work-order history is archived, never erased. */
@Component({
  selector: 'app-vehicles',
  imports: [RouterLink, FormsModule, VehicleForm],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './vehicles.html',
  styleUrl: './vehicles.scss',
})
export class VehiclesPage {
  private readonly http = inject(HttpClient);

  protected readonly search = signal('');
  protected readonly includeArchived = signal(false);
  protected readonly page = signal(1);
  protected readonly data = signal<PaginatedList<VehicleListItem> | null>(null);
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);
  protected readonly alert = signal<string | null>(null);

  protected readonly formOpen = signal(false);
  protected readonly editing = signal<VehicleListItem | null>(null);
  protected readonly formBusy = signal(false);
  protected readonly formError = signal<string | null>(null);

  // Create mode needs an owner; the picker loads the customers book once per open.
  protected readonly pickingCustomer = signal(false);
  protected readonly ownerId = signal('');
  protected readonly owners = signal<{ customerId: string; name: string }[]>([]);
  protected readonly ownersBusy = signal(false);

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.pageError.set(null);

    const params = new URLSearchParams({
      page: String(this.page()),
      pageSize: String(PAGE_SIZE),
      includeArchived: String(this.includeArchived()),
    });

    const search = this.search().trim();

    if (search.length > 0) {
      params.set('search', search);
    }

    this.http
      .get<PaginatedList<VehicleListItem>>(`/api/vehicles?${params}`)
      .subscribe({
        next: data => {
          this.data.set(data);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.pageError.set(problemMessage(error));
          this.loading.set(false);
        },
      });
  }

  protected startSearch(): void {
    this.page.set(1);
    this.load();
  }

  protected toggleArchived(include: boolean): void {
    this.includeArchived.set(include);
    this.page.set(1);
    this.load();
  }

  protected goToPage(page: number): void {
    this.page.set(page);
    this.load();
  }

  // --- dialogs ---------------------------------------------------------------

  protected openCreate(): void {
    this.editing.set(null);
    this.formError.set(null);
    this.ownerId.set('');
    this.owners.set([]);
    this.pickingCustomer.set(true);
    this.loadOwners();
    this.formOpen.set(true);
  }

  protected openEdit(vehicle: VehicleListItem): void {
    this.editing.set(vehicle);
    this.formError.set(null);
    this.formOpen.set(true);
  }

  protected closeForm(): void {
    this.formOpen.set(false);
  }

  protected save(payload: VehiclePayload): void {
    const editing = this.editing();
    this.formBusy.set(true);
    this.formError.set(null);

    // Create mode needs the owner picked beside the form; keep the API call off
    // the wire until it is.
    if (!editing && this.ownerId() === '') {
      this.formBusy.set(false);
      this.formError.set('Pick the owner of this car first.');
      return;
    }

    const request$ = editing
      ? this.http.put(`/api/vehicles/${editing.vehicleId}`, payload)
      : this.http.post(`/api/customers/${this.ownerId()}/vehicles`, payload);

    request$.subscribe({
      next: () => {
        this.formBusy.set(false);
        this.formOpen.set(false);
        this.pickingCustomer.set(false);
        this.alert.set(null);
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.formBusy.set(false);
        this.formError.set(problemMessage(error));
      },
    });
  }

  private loadOwners(): void {
    this.ownersBusy.set(true);

    this.http
      .get<PaginatedList<{ customerId: string; name: string }>>('/api/customers?page=1&pageSize=100')
      .subscribe({
        next: data => {
          this.owners.set(data.items.map(c => ({ customerId: c.customerId, name: c.name })));
          this.ownersBusy.set(false);
        },
        error: () => this.ownersBusy.set(false),
      });
  }

  // --- row actions -------------------------------------------------------------

  protected archive(vehicle: VehicleListItem): void {
    this.http.put(`/api/vehicles/${vehicle.vehicleId}/archive`, {}).subscribe({
      next: () => {
        this.alert.set(`"${vehicle.licensePlate}" archived — hidden from live lists, history intact.`);
        this.load();
      },
      error: (error: HttpErrorResponse) => this.alert.set(problemMessage(error)),
    });
  }

  protected restore(vehicle: VehicleListItem): void {
    this.http.put(`/api/vehicles/${vehicle.vehicleId}/restore`, {}).subscribe({
      next: () => {
        this.alert.set(`"${vehicle.licensePlate}" back in service.`);
        this.load();
      },
      error: (error: HttpErrorResponse) => this.alert.set(problemMessage(error)),
    });
  }

  protected remove(vehicle: VehicleListItem): void {
    if (!window.confirm(`Delete "${vehicle.licensePlate}"?`)) {
      return;
    }

    this.http.delete(`/api/vehicles/${vehicle.vehicleId}`).subscribe({
      next: () => {
        this.alert.set(`"${vehicle.licensePlate}" removed. Cars with history get archived instead.`);
        this.load();
      },
      error: (error: HttpErrorResponse) => this.alert.set(problemMessage(error)),
    });
  }
}
