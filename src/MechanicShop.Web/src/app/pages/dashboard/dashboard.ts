import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { EmployeeSummary, PaginatedList, WorkOrderSummary, workOrderStateLabel } from '../../core/types';
import { fmtDate, fmtMinute } from '../../core/format';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class DashboardPage {
  private readonly http = inject(HttpClient);

  protected readonly loaded = signal(false);
  protected readonly workOrderCount = signal(0);
  protected readonly technicianCount = signal(0);
  protected readonly serviceCount = signal(0);
  protected readonly customerCount = signal(0);
  protected readonly recent = signal<WorkOrderSummary[]>([]);

  protected readonly fmtDate = fmtDate;
  protected readonly fmtMinute = fmtMinute;
  protected readonly stateLabel = workOrderStateLabel;

  ngOnInit(): void {
    forkJoin({
      orders: this.http.get<PaginatedList<WorkOrderSummary>>('/api/work-orders?page=1&pageSize=6'),
      services: this.http.get<PaginatedList<{ serviceTaskId: string }>>('/api/service-tasks?page=1&pageSize=1'),
      customers: this.http.get<PaginatedList<{ customerId: string }>>('/api/customers?page=1&pageSize=1'),
      employees: this.http.get<EmployeeSummary[]>('/api/employees'),
    }).subscribe({
      next: ({ orders, services, customers, employees }) => {
        this.workOrderCount.set(orders.totalCount);
        this.serviceCount.set(services.totalCount);
        this.customerCount.set(customers.totalCount);
        this.technicianCount.set(employees.filter(e => e.role === 0).length);
        this.recent.set(orders.items);
        this.loaded.set(true);
      },
      error: () => this.loaded.set(true),
    });
  }
}
