import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { forkJoin } from 'rxjs';

import { PartOrderRow, PartRow, partCategoryLabel, partStockLabel } from '../../core/types';
import { problemMessage } from '../../core/api';
import { AuthStore } from '../../core/auth-store';

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
  protected readonly auth = inject(AuthStore);

  protected readonly parts = signal<PartRow[]>([]);
  protected readonly orders = signal<PartOrderRow[]>([]);
  protected readonly suppliers = signal<SupplierRow[]>([]);
  protected readonly purchaseOrders = signal<PurchaseOrderRow[]>([]);
  protected readonly movements = signal<StockMovementRow[]>([]);
  protected readonly historyPartId = signal<string | null>(null);
  protected readonly historyLoading = signal(false);
  protected readonly search = signal('');
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);
  protected readonly alert = signal<string | null>(null);
  protected readonly busyPart = signal<string | null>(null);
  protected readonly busyOrder = signal<string | null>(null);
  protected readonly busyPurchaseOrder = signal<string | null>(null);
  protected readonly supplierName = signal('');
  protected readonly supplierEmail = signal('');
  protected readonly selectedSupplierId = signal('');
  protected readonly purchasePartId = signal('');
  protected readonly purchaseQuantity = signal(1);
  protected readonly purchaseUnitCost = signal(1);
  protected readonly canCreatePurchaseOrder = computed(() => {
    const quantity = this.purchaseQuantity();
    const unitCost = this.purchaseUnitCost();
    return Number.isInteger(quantity) && quantity >= 1 && quantity <= 100000 && Number.isFinite(unitCost) && unitCost > 0;
  });

  protected readonly fmtMovementDate = (value: string) => new Date(value).toLocaleString();
  protected readonly stockLabel = partStockLabel;
  protected readonly partCategoryLabel = partCategoryLabel;
  protected readonly visibleParts = computed(() => {
    const query = this.search().trim().toLocaleLowerCase();
    return query ? this.parts().filter(part =>
      part.name.toLocaleLowerCase().includes(query) ||
      this.partCategoryLabel(part.category).toLocaleLowerCase().includes(query)) : this.parts();
  });

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

    forkJoin({
      parts: this.http.get<PartRow[]>('/api/parts'),
      shopOrders: this.http.get<Paginated<PartOrderRow>>('/api/shop-orders?page=1&pageSize=20'),
      suppliers: this.http.get<SupplierRow[]>('/api/suppliers'),
      purchaseOrders: this.http.get<PurchaseOrderRow[]>('/api/purchase-orders'),
    }).subscribe({
      next: data => {
        this.parts.set(data.parts);
        this.orders.set(data.shopOrders.items);
        this.suppliers.set(data.suppliers);
        this.purchaseOrders.set(data.purchaseOrders);
        if (!this.selectedSupplierId() && data.suppliers.length) this.selectedSupplierId.set(data.suppliers[0].supplierId);
        if (!this.purchasePartId() && data.parts.length) this.purchasePartId.set(data.parts[0].partId);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.pageError.set(problemMessage(error));
        this.loading.set(false);
      },
    });
  }

  protected createSupplier(): void {
    const name = this.supplierName().trim();
    if (!name) return;
    this.http.post<SupplierRow>('/api/suppliers', { name, email: this.supplierEmail().trim() || null })
      .subscribe({
        next: () => {
          this.supplierName.set('');
          this.supplierEmail.set('');
          this.alert.set('Supplier added.');
          this.load();
        },
        error: (error: HttpErrorResponse) => this.alert.set(problemMessage(error) ?? 'Could not add supplier.'),
      });
  }

  protected createPurchaseOrder(): void {
    const supplierId = this.selectedSupplierId();
    const partId = this.purchasePartId();
    const quantity = this.purchaseQuantity();
    const unitCost = this.purchaseUnitCost();
    if (!supplierId || !partId || !Number.isInteger(quantity) || quantity < 1 || quantity > 100000 || !Number.isFinite(unitCost) || unitCost <= 0) return;
    this.http.post<PurchaseOrderRow>('/api/purchase-orders', {
      supplierId,
      items: [{ partId, quantity, unitCost }],
    }).subscribe({
      next: order => {
        this.alert.set(`Purchase order ${order.number} created.`);
        this.load();
      },
      error: (error: HttpErrorResponse) => this.alert.set(problemMessage(error) ?? 'Could not create purchase order.'),
    });
  }

  protected receivePurchaseOrder(order: PurchaseOrderRow): void {
    if (order.status !== 0 || !window.confirm(`Receive all ${order.lines.length} line(s) on ${order.number}? This will add them to stock.`)) return;
    this.busyPurchaseOrder.set(order.purchaseOrderId);
    this.http.post(`/api/purchase-orders/${order.purchaseOrderId}/receive`, {}).subscribe({
      next: () => {
        this.busyPurchaseOrder.set(null);
        this.alert.set(`${order.number} received and stock updated.`);
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.busyPurchaseOrder.set(null);
        this.alert.set(problemMessage(error) ?? 'Could not receive purchase order.');
      },
    });
  }

  protected adjust(part: PartRow, delta: number): void {
    this.busyPart.set(part.partId);
    this.alert.set(null);

    const reason = delta > 0 ? 'Manual restock' : 'Manual stock-out';
    this.http.post(`/api/parts/${part.partId}/adjust`, { delta, reason }).subscribe({
      next: () => {
        this.busyPart.set(null);
        this.alert.set(delta > 0
          ? `Restocked ${part.name} (+${delta}).`
          : `Moved ${part.name} (${delta}).`);
        this.load();
        if (this.historyPartId() === part.partId) this.loadMovementHistory(part.partId);
      },
      error: (error: HttpErrorResponse) => {
        this.busyPart.set(null);
        this.alert.set(problemMessage(error) ?? 'The stock movement failed.');
      },
    });
  }

  protected toggleHistory(part: PartRow): void {
    if (this.historyPartId() === part.partId) {
      this.historyPartId.set(null);
      this.movements.set([]);
      return;
    }
    this.historyPartId.set(part.partId);
    this.loadMovementHistory(part.partId);
  }

  private loadMovementHistory(partId: string): void {
    this.historyLoading.set(true);
    this.http.get<StockMovementRow[]>(`/api/parts/${partId}/movements`).subscribe({
      next: rows => { this.movements.set(rows); this.historyLoading.set(false); },
      error: (error: HttpErrorResponse) => {
        this.movements.set([]);
        this.historyLoading.set(false);
        this.alert.set(problemMessage(error) ?? 'Could not load stock history.');
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

interface StockMovementRow {
  movementId: string;
  partId: string;
  delta: number;
  quantityBefore: number;
  quantityAfter: number;
  reason: string;
  actorUserId: string | null;
  recordedAt: string;
}

interface SupplierRow {
  supplierId: string;
  name: string;
  contactName: string | null;
  email: string | null;
  phone: string | null;
  isActive: boolean;
}

interface PurchaseOrderRow {
  purchaseOrderId: string;
  number: string;
  supplierId: string;
  supplierName: string;
  orderedOn: string;
  receivedOn: string | null;
  status: number;
  subtotal: number;
  lines: { partId: string; partName: string; quantity: number; unitCost: number; lineTotal: number }[];
}
