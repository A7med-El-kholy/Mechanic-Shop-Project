import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';

import { InvoiceRow, PaginatedList, invoiceStatusLabel } from '../../core/types';
import { fmtDate } from '../../core/format';
import { problemMessage } from '../../core/api';

const PAGE_SIZE = 10;

/** The billing book. Rows appear the moment a job completes (the API issues
 * them — issuance is not a button); the row leads to the printable detail. */
@Component({
  selector: 'app-invoices',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './invoices.html',
  styleUrl: './invoices.scss',
})
export class InvoicesPage {
  private readonly http = inject(HttpClient);

  protected readonly page = signal(1);
  protected readonly data = signal<PaginatedList<InvoiceRow> | null>(null);
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);

  protected readonly fmtDate = fmtDate;
  protected readonly stateLabel = invoiceStatusLabel;

  /** Invoice statuses borrow the badge palette: 0/2 = attention, 1 = paid green. */
  protected readonly badgeKind = (status: number) => (status === 1 ? 'badge--1' : 'badge--0');

  protected readonly money = new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  });

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.pageError.set(null);

    this.http
      .get<PaginatedList<InvoiceRow>>(`/api/invoices?page=${this.page()}&pageSize=${PAGE_SIZE}`)
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
