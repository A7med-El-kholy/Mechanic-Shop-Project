import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { problemMessage } from '../../core/api';
import { AuthStore } from '../../core/auth-store';
import { fmtDate, fmtMinute, workOrderRef } from '../../core/format';
import {
  PaginatedList,
  WORK_ORDER_STATES,
  WorkOrderSummary,
  workOrderStateLabel,
} from '../../core/types';

const PAGE_SIZE = 10;

/** The work-orders board: the job's day, bay and crew on one line, filtered by
 * the status the shop floor is asking about. Opening a row is the only write —
 * transitions live on the ticket so a mis-click never starts someone's job. */
@Component({
  selector: 'app-work-orders',
  imports: [RouterLink, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './work-orders.html',
  styleUrl: './work-orders.scss',
})
export class WorkOrdersPage {
  private readonly http = inject(HttpClient);
  protected readonly auth = inject(AuthStore);

  protected readonly data = signal<PaginatedList<WorkOrderSummary> | null>(null);
  protected readonly page = signal(1);
  protected readonly stateFilter = signal('');
  protected readonly search = signal('');
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);

  protected readonly states = WORK_ORDER_STATES;
  protected readonly stateLabel = workOrderStateLabel;
  protected readonly fmtDate = fmtDate;
  protected readonly fmtMinute = fmtMinute;
  protected readonly ref = workOrderRef;

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.pageError.set(null);

    const params = new URLSearchParams({
      page: String(this.page()),
      pageSize: String(PAGE_SIZE),
    });

    const search = this.search().trim();

    if (search.length > 0) {
      params.set('search', search);
    }

    if (this.stateFilter() !== '') {
      params.set('state', this.stateFilter());
    }

    this.http
      .get<PaginatedList<WorkOrderSummary>>(`/api/work-orders?${params}`)
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

  protected applyFilters(): void {
    this.page.set(1);
    this.load();
  }

  protected clearFilters(): void {
    this.search.set('');
    this.stateFilter.set('');
    this.applyFilters();
  }

  protected goToPage(page: number): void {
    this.page.set(page);
    this.load();
  }
}
