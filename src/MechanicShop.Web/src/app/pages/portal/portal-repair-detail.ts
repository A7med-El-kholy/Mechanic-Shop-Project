import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';

import { RepairDetail, workOrderStateLabel } from '../../core/types';
import { fmtDate, fmtMinute } from '../../core/format';
import { problemMessage } from '../../core/api';

/** The customer's repair ticket: what the job covers, when it runs, where it
 * sits, and its running total. Prices shown come from the same WorkOrderDto the
 * staff screens read — one source of truth. */
@Component({
  selector: 'app-portal-repair-detail',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './portal-repair-detail.html',
  styleUrl: './portal-repair-detail.scss',
})
export class PortalRepairDetailPage {
  private readonly http = inject(HttpClient);

  readonly repairId = input.required<string>();

  protected readonly repair = signal<RepairDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);

  protected readonly fmtDate = fmtDate;
  protected readonly fmtMinute = fmtMinute;
  protected readonly stateLabel = workOrderStateLabel;

  protected readonly money = new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  });

  constructor() {
    effect(() => {
      this.repairId();
      this.load();
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.pageError.set(null);

    this.http
      .get<RepairDetail>(`/api/portal/repairs/${this.repairId()}`)
      .subscribe({
        next: repair => {
          this.repair.set(repair);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          // Someone else's ticket id reads as not found — deliberate.
          this.pageError.set(problemMessage(error));
          this.loading.set(false);
        },
      });
  }
}
