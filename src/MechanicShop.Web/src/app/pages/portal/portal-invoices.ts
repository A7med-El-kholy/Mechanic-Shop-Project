import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';

import { InvoiceRow, invoiceStatusLabel } from '../../core/types';
import { fmtDate } from '../../core/format';
import { problemMessage } from '../../core/api';

/** The customer's invoices. They can choose cash at the shop; only the workshop
 * can close the invoice as paid after confirming receipt. */
@Component({
  selector: 'app-portal-invoices',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './portal-invoices.html',
  styleUrl: './portal-invoices.scss',
})
export class PortalInvoicesPage {
  private readonly http = inject(HttpClient);

  protected readonly invoices = signal<InvoiceRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);

  protected readonly fmtDate = fmtDate;
  protected readonly stateLabel = invoiceStatusLabel;
  protected readonly badgeKind = (status: number) => (status === 1 ? 'badge--1' : 'badge--0');

  protected readonly money = new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  });

  ngOnInit(): void {
    this.http
      .get<InvoiceRow[]>('/api/portal/invoices')
      .subscribe({
        next: invoices => {
          this.invoices.set(invoices);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.pageError.set(problemMessage(error));
          this.loading.set(false);
        },
      });
  }
}
