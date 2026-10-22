import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';

import { PartOrderRow, PartRow, partCategoryLabel, partStockLabel } from '../../core/types';
import { fmtDate } from '../../core/format';
import { problemMessage } from '../../core/api';

/** The storeroom: shelves and their levels, the manager's movement buttons,
 * and beneath it the order book of what customers bought from the shop. The
 * prototype's rule holds: stock signs reflect reality — movements never go
 * negative, and completing a job consumes its part automatically (API side). */
@Component({
  selector: 'app-inventory',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './inventory.html',
  styleUrl: './inventory.scss',
})
export class InventoryPage {
  private readonly http = inject(HttpClient);

  protected readonly parts = signal<PartRow[]>([]);
  protected readonly orders = signal<PartOrderRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);
  protected readonly alert = signal<string | null>(null);
  protected readonly busyPart = signal<string | null>(null);
  protected readonly busyOrder = signal<string | null>(null);

  protected readonly fmtDate = fmtDate;
  protected readonly stockLabel = partStockLabel;
  protected readonly partCategoryLabel = partCategoryLabel;

  /** Borrow the badge palette: green / warn / red — 0/1/2 → 1/0/3. */
  protected readonly badgeKind = (state: number) => (state === 0 ? 'badge--1' : state === 1 ? 'badge--0' : 'badge--3');

  protected readonly orderBadgeKind = (status: number) => (status === 0 ? 'badge--1' : 'badge--0');

  protected readonly lowCount = computed(
    () => this.parts().filter(p => p.stockState !== 0).length);

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

    this.http.get<PartRow[]>('/api/parts').subscribe({
      next: parts => {
        this.parts.set(parts);
        this.http.get<Paginated<PartOrderRow>>('/api/shop-orders?page=1&pageSize=20').subscribe({
          next: orders => {
            this.orders.set(orders.items);
            this.loading.set(false);
          },
          error: (error: HttpErrorResponse) => {
            this.pageError.set(problemMessage(error));
            this.loading.set(false);
          },
        });
      },
      error: (error: HttpErrorResponse) => {
        this.pageError.set(problemMessage(error));
        this.loading.set(false);
      },
    });
  }

  protected adjust(part: PartRow, delta: number): void {
    this.busyPart.set(part.partId);
    this.alert.set(null);

    this.http.post(`/api/parts/${part.partId}/adjust`, { delta }).subscribe({
      next: () => {
        this.busyPart.set(null);
        this.alert.set(delta > 0
          ? `Restocked ${part.name} (+${delta}).`
          : `Moved ${part.name} (${delta}).`);
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.busyPart.set(null);
        this.alert.set(problemMessage(error) ?? 'The stock movement failed.');
      },
    });
  }

  protected recordCash(order: PartOrderRow): void {
    if (!window.confirm(`Cash received for order #${order.orderNumber}? It will be marked Paid.`)) {
      return;
    }

    this.busyOrder.set(order.orderId);
    this.alert.set(null);

    this.http.post(`/api/shop-orders/${order.orderId}/record-cash-payment`, {}).subscribe({
      next: () => {
        this.busyOrder.set(null);
        this.alert.set(`Cash recorded — order #${order.orderNumber} is now Paid.`);
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.busyOrder.set(null);
        this.alert.set(problemMessage(error) ?? 'Could not record the payment.');
      },
    });
  }
}

interface Paginated<T> {
  items: T[];
  totalPages: number;
  totalCount: number;
}
