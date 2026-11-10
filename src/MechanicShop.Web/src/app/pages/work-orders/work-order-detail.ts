import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';

import { problemMessage } from '../../core/api';
import { AuthStore } from '../../core/auth-store';
import { fmtDate, fmtMinute, workOrderRef } from '../../core/format';
import {
  InvoiceRow,
  PaginatedList,
  WorkOrderDetail,
  workOrderStateLabel,
} from '../../core/types';

/** One ticket: the car, the crew, the day, the money, and the only three moves
 * the state machine allows. Completing it issues the invoice on the server —
 * the button does not create paperwork, it triggers the rule that does. */
@Component({
  selector: 'app-work-order-detail',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './work-order-detail.html',
  styleUrl: './work-order-detail.scss',
})
export class WorkOrderDetailPage {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthStore);

  readonly workOrderId = input.required<string>();

  protected readonly order = signal<WorkOrderDetail | null>(null);
  protected readonly invoice = signal<InvoiceRow | null>(null);
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);
  protected readonly busy = signal(false);

  protected readonly fmtDate = fmtDate;
  protected readonly fmtMinute = fmtMinute;
  protected readonly ref = workOrderRef;
  protected readonly stateLabel = workOrderStateLabel;

  protected readonly money = new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  });

  constructor() {
    effect(() => {
      this.workOrderId();
      this.load();
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.pageError.set(null);

    this.http
      .get<WorkOrderDetail>(`/api/work-orders/${this.workOrderId()}`)
      .subscribe({
        next: order => {
          this.order.set(order);
          this.loading.set(false);
          this.loadInvoice();
        },
        error: (error: HttpErrorResponse) => {
          this.pageError.set(problemMessage(error));
          this.loading.set(false);
        },
      });
  }

  /** Completion issues at most one invoice; the filter asks for exactly that
   * row instead of paging the billing book. The link is optional furniture —
   * a failure here must not block the ticket. */
  private loadInvoice(): void {
    this.http
      .get<PaginatedList<InvoiceRow>>(
        `/api/invoices?workOrderId=${this.workOrderId()}&pageSize=1`)
      .subscribe({
        next: page => this.invoice.set(page.items[0] ?? null),
        error: () => this.invoice.set(null),
      });
  }

  protected setState(state: number): void {
    if (state === 3 && !window.confirm('Cancel this work order? The customer will be notified.')) {
      return;
    }

    this.busy.set(true);
    this.actionError.set(null);

    this.http
      .put<void>(`/api/work-orders/${this.workOrderId()}/status`, { state })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.load();
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.actionError.set(problemMessage(error));
        },
      });
  }

  protected remove(): void {
    if (!window.confirm('Delete this scheduled work order? This cannot be undone.')) {
      return;
    }

    this.busy.set(true);
    this.actionError.set(null);

    this.http.delete<void>(`/api/work-orders/${this.workOrderId()}`).subscribe({
      next: () => {
        this.busy.set(false);
        void this.router.navigate(['/work-orders']);
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.actionError.set(problemMessage(error));
      },
    });
  }
}
