import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';

import { problemMessage } from '../../core/api';
import { AuthStore } from '../../core/auth-store';
import { fmtDate, fmtMinute, todayIso, workOrderRef } from '../../core/format';
import { PaginatedList, WorkOrderSummary, workOrderStateLabel } from '../../core/types';

/** My Dashboard, the prototype's dT(): the technician's day on one screen —
 * assigned, in progress, next job — then the day's bookings as cards. The API
 * already narrows the list to the caller's own hands, so this page is a view,
 * never a filter. */
@Component({
  selector: 'app-tech-home',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './tech-home.html',
  styleUrl: './tech-home.scss',
})
export class TechHomePage {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthStore);

  protected readonly day = signal(todayIso());
  protected readonly orders = signal<WorkOrderSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);

  protected readonly fmtDate = fmtDate;
  protected readonly fmtMinute = fmtMinute;
  protected readonly ref = workOrderRef;
  protected readonly stateLabel = workOrderStateLabel;

  protected readonly greeting = computed(() => {
    const first = this.auth.fullName().split(/\s+/)[0] ?? '';

    return `Good day, ${first || 'there'}`;
  });

  protected readonly kpis = computed(() => {
    const jobs = this.orders();
    const next = jobs
      .filter(job => job.state === 0)
      .sort((a, b) => a.startsAtMinute - b.startsAtMinute)[0];

    return {
      assigned: jobs.length,
      inProgress: jobs.filter(job => job.state === 1).length,
      nextJob: next ? fmtMinute(next.startsAtMinute) : '—',
    };
  });

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.pageError.set(null);

    const date = this.day();

    this.http
      .get<PaginatedList<WorkOrderSummary>>(
        `/api/work-orders?scheduledDate=${encodeURIComponent(date)}&pageSize=100`)
      .subscribe({
        next: page => {
          this.orders.set(page.items);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.pageError.set(problemMessage(error));
          this.loading.set(false);
        },
      });
  }

  protected gotoSelf(): void {
    this.day.set(todayIso());
    this.load();
  }
}
