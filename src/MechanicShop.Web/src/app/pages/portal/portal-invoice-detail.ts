import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';

import { InvoiceDetail, invoiceStatusLabel } from '../../core/types';
import { fmtDate } from '../../core/format';
import { problemMessage } from '../../core/api';

/** The customer's bill. Two honest doors per the prototype: pay by card —
 * marked paid on the spot, a demo click with no card data anywhere — or
 * choose cash at the shop, which parks the invoice in cash-pending until the
 * workshop confirms receipt. */
@Component({
  selector: 'app-portal-invoice-detail',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './portal-invoice-detail.html',
  styleUrl: './portal-invoice-detail.scss',
})
export class PortalInvoiceDetailPage {
  private readonly http = inject(HttpClient);

  readonly invoiceId = input.required<string>();

  protected readonly invoice = signal<InvoiceDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);
  protected readonly alert = signal<string | null>(null);
  protected readonly alertKind = signal<'ok' | 'err'>('ok');
  protected readonly busy = signal(false);

  protected readonly fmtDate = fmtDate;
  protected readonly stateLabel = invoiceStatusLabel;
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
      .get<InvoiceDetail>(`/api/portal/invoices/${this.invoiceId()}`)
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

  protected pay(method: 'card' | 'cash'): void {
    const invoice = this.invoice();

    if (!invoice) {
      return;
    }

    if (method === 'card' &&
        !window.confirm(`Pay invoice #${invoice.invoiceNumber} (${this.money.format(invoice.totalAmount)}) by card? Prototype only — no real payment is processed.`)) {
      return;
    }

    this.busy.set(true);
    this.alert.set(null);

    this.http
      .post(`/api/portal/invoices/${invoice.invoiceId}/pay`, { method: method === 'card' ? 0 : 1 })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.alertKind.set('ok');
          this.alert.set(method === 'card'
            ? 'Payment received — thank you!'
            : 'Cash at the shop selected. Pay when you collect your vehicle.');
          this.load();
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.alertKind.set('err');
          this.alert.set(problemMessage(error) ?? 'The payment could not be recorded.');
        },
      });
  }
}
