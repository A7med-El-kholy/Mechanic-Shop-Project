import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';

import { InvoiceDetail, invoiceStatusLabel } from '../../core/types';
import { fmtDate } from '../../core/format';
import { problemMessage } from '../../core/api';

/** One invoice, printable straight from the browser (Ctrl+P). Payment story:
 * unpaid shows nothing but awareness, cash-pending offers "record cash", and
 * a paid document keeps its method of record. */
@Component({
  selector: 'app-invoice-detail',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './invoice-detail.html',
  styleUrl: './invoice-detail.scss',
})
export class InvoiceDetailPage {
  private readonly http = inject(HttpClient);

  readonly invoiceId = input.required<string>();

  protected readonly invoice = signal<InvoiceDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);
  protected readonly alert = signal<string | null>(null);
  protected readonly busy = signal(false);

  protected readonly fmtDate = fmtDate;
  protected readonly stateLabel = invoiceStatusLabel;

  /** Invoice statuses borrow the badge palette: 0/2 = attention, 1 = paid green. */
  protected readonly badgeKind = (status: number) => (status === 1 ? 'badge--1' : 'badge--0');

  protected readonly taxRateLabel = computed(
    () => `${Math.round((this.invoice()?.taxRate ?? 0) * 100)}%`);

  protected readonly money = new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  });

  constructor() {
    effect(() => {
      this.invoiceId();
      this.load();
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.pageError.set(null);

    this.http
      .get<InvoiceDetail>(`/api/invoices/${this.invoiceId()}`)
      .subscribe({
        next: invoice => {
          this.invoice.set(invoice);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.pageError.set(problemMessage(error));
          this.loading.set(false);
        },
      });
  }

  protected recordCash(): void {
    const invoice = this.invoice();

    if (!invoice) {
      return;
    }

    if (!window.confirm(`Confirm cash received for invoice #${invoice.invoiceNumber}? It will be marked Paid.`)) {
      return;
    }

    this.busy.set(true);
    this.alert.set(null);

    this.http
      .post(`/api/invoices/${invoice.invoiceId}/record-cash-payment`, {})
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.alert.set('Cash recorded — invoice is now Paid.');
          this.load();
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.alert.set(problemMessage(error) ?? 'Could not record the payment.');
        },
      });
  }
}
