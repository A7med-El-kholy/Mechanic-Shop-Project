import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { problemMessage } from '../../core/api';
import { fmtMinute } from '../../core/format';
import {
  EmployeeSummary,
  PaginatedList,
  ServiceOption,
  VehicleListItem,
  WorkOrderDetail,
} from '../../core/types';

const VEHICLE_PICKER_SIZE = 8;

/** Booking a job: vehicle first (the shop's search reaches plate, VIN and owner),
 * then the services, then the day/bay/technician. The estimate beside the form is
 * the same arithmetic the API will apply — the server stays the authority, this
 * only tells the writer what they are about to book. */
@Component({
  selector: 'app-work-order-new',
  imports: [RouterLink, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './work-order-new.html',
  styleUrl: './work-order-new.scss',
})
export class WorkOrderNewPage {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  protected readonly vehicles = signal<VehicleListItem[]>([]);
  protected readonly vehicleSearch = signal('');
  protected readonly vehiclesLoading = signal(true);
  protected readonly vehiclesError = signal<string | null>(null);

  protected readonly services = signal<ServiceOption[]>([]);
  protected readonly servicesLoading = signal(true);
  protected readonly servicesError = signal<string | null>(null);
  protected readonly selectedServiceIds = signal<ReadonlySet<string>>(new Set());

  protected readonly technicians = signal<EmployeeSummary[]>([]);
  protected readonly techniciansLoading = signal(true);

  protected readonly selectedVehicleId = signal<string | null>(null);
  protected readonly technicianId = signal('');
  protected readonly scheduledDate = signal('');
  protected readonly startsAtMinute = signal(480);
  protected readonly spot = signal(0);

  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);

  /** 08:00 → 19:45 on the workshop's 15-minute start grid. */
  protected readonly slots = Array.from({ length: 48 }, (_, index) => 480 + index * 15);

  protected readonly today = (() => {
    const now = new Date();

    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
  })();

  protected readonly bays = [
    { value: 0, label: 'Bay A' },
    { value: 1, label: 'Bay B' },
    { value: 2, label: 'Bay C' },
  ];

  protected readonly fmtMinute = fmtMinute;

  protected readonly money = new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  });

  protected readonly selectedVehicle = computed(
    () => this.vehicles().find(v => v.vehicleId === this.selectedVehicleId()) ?? null);

  protected readonly selectedServices = computed(() => {
    const ids = this.selectedServiceIds();

    return this.services().filter(service => ids.has(service.serviceTaskId));
  });

  protected readonly estimatedTotal = computed(
    () => this.selectedServices().reduce((sum, service) => sum + service.basePrice, 0));

  protected readonly estimatedDuration = computed(
    () => this.selectedServices().reduce((sum, service) => sum + service.estimatedDurationInMins, 0));

  protected readonly estimatedEnd = computed(
    () => this.startsAtMinute() + this.estimatedDuration());

  ngOnInit(): void {
    this.searchVehicles();

    this.http
      .get<PaginatedList<ServiceOption>>('/api/service-tasks?page=1&pageSize=100')
      .subscribe({
        next: data => {
          this.services.set(data.items);
          this.servicesLoading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.servicesError.set(problemMessage(error));
          this.servicesLoading.set(false);
        },
      });

    this.http.get<EmployeeSummary[]>('/api/employees/technicians').subscribe({
      next: technicians => {
        this.technicians.set(technicians);
        this.techniciansLoading.set(false);
      },
      error: () => this.techniciansLoading.set(false),
    });
  }

  protected searchVehicles(): void {
    this.vehiclesLoading.set(true);
    this.vehiclesError.set(null);

    const params = new URLSearchParams({
      page: '1',
      pageSize: String(VEHICLE_PICKER_SIZE),
    });

    const search = this.vehicleSearch().trim();

    if (search.length > 0) {
      params.set('search', search);
    }

    this.http
      .get<PaginatedList<VehicleListItem>>(`/api/vehicles?${params}`)
      .subscribe({
        next: data => {
          this.vehicles.set(data.items);
          this.vehiclesLoading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.vehiclesError.set(problemMessage(error));
          this.vehiclesLoading.set(false);
        },
      });
  }

  protected toggleService(serviceTaskId: string): void {
    this.selectedServiceIds.update(current => {
      const next = new Set(current);

      if (next.has(serviceTaskId)) {
        next.delete(serviceTaskId);
      } else {
        next.add(serviceTaskId);
      }

      return next;
    });
  }

  protected create(): void {
    const vehicleId = this.selectedVehicleId();
    const serviceTaskIds = [...this.selectedServiceIds()];
    const technicianId = this.technicianId();
    const scheduledDate = this.scheduledDate();

    if (!vehicleId) {
      this.saveError.set('Pick the vehicle this job is for.');
      return;
    }

    if (serviceTaskIds.length === 0) {
      this.saveError.set('Pick at least one service.');
      return;
    }

    if (!technicianId) {
      this.saveError.set('Pick the technician who will do the work.');
      return;
    }

    if (!scheduledDate) {
      this.saveError.set('Pick the day the job starts.');
      return;
    }

    this.saving.set(true);
    this.saveError.set(null);

    this.http
      .post<WorkOrderDetail>('/api/work-orders', {
        vehicleId,
        serviceTaskIds,
        scheduledDate,
        startsAtMinute: this.startsAtMinute(),
        spot: this.spot(),
        technicianId,
      })
      .subscribe({
        next: created => {
          this.saving.set(false);
          void this.router.navigate(['/work-orders', created.workOrderId]);
        },
        error: (error: HttpErrorResponse) => {
          this.saving.set(false);
          this.saveError.set(problemMessage(error));
        },
      });
  }
}
