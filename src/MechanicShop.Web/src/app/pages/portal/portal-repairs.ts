import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';

import { PaginatedList, WorkOrderSummary, workOrderStateLabel } from '../../core/types';
import { fmtDate, fmtMinute } from '../../core/format';
import { problemMessage } from '../../core/api';

const PAGE_SIZE = 10;

/** "My Repairs" — every job the shop ever wrote for this account's cars,
 * newest first. Read-only by design: state changes belong to the workshop. */
@Component({
  selector: 'app-portal-repairs',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './portal-repairs.html',
  styleUrl: './portal-repairs.scss',
})
export class PortalRepairsPage {
  private readonly http = inject(HttpClient);

  protected readonly page = signal(1);
  protected readonly data = signal<PaginatedList<WorkOrderSummary> | null>(null);
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);

  protected readonly fmtDate = fmtDate;
  protected readonly fmtMinute = fmtMinute;
  protected readonly stateLabel = workOrderStateLabel;

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.pageError.set(null);

    this.http
      .get<PaginatedList<WorkOrderSummary>>(
        `/api/portal/repairs?page=${this.page()}&pageSize=${PAGE_SIZE}`)
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

  protected goToPage(page: number): void {
    this.page.set(page);
    this.load();
  }
}
