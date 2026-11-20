import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';

import { PART_CATEGORIES, PartOrderRow, PartRow, partStockLabel } from '../../core/types';
import { fmtDate } from '../../core/format';
import { problemMessage } from '../../core/api';

/** The customer's parts shop, straight from the prototype: filtered grid with
 * stock badges, a cart with the 15% tax math, checkout reserved for cash at
 * pickup (the workshop closes it later). Every successful
 * order already deducts stock server-side; out-of-stock stays un-buyable. */
@Component({
  selector: 'app-portal-shop',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shop.html',
  styleUrl: './shop.scss',
})
export class PortalShopPage {
  private readonly http = inject(HttpClient);

  protected readonly categories = ['All', ...PART_CATEGORIES.map(c => c.label)];

  protected readonly parts = signal<PartRow[]>([]);
  protected readonly myOrders = signal<PartOrderRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);
  protected readonly alert = signal<string | null>(null);
  protected readonly alertKind = signal<'ok' | 'err'>('ok');
  protected readonly cart = signal<Record<string, number>>({});
  protected readonly selectedCategory = signal('All');
  protected readonly placing = signal(false);

  protected readonly fmtDate = fmtDate;
  protected readonly stockLabel = partStockLabel;
  protected readonly badgeKind = (state: number) => (state === 0 ? 'badge--1' : state === 1 ? 'badge--0' : 'badge--3');
  protected readonly orderBadgeKind = (status: number) => (status === 0 ? 'badge--1' : 'badge--0');

  protected readonly filteredParts = computed(() => {
    const category = this.selectedCategory();

    return this.parts().filter(p =>
      category === 'All' || PART_CATEGORIES.find(c => c.label === category)?.value === p.category);
  });

  protected readonly cartCount = computed(
    () => Object.values(this.cart()).reduce((sum, quantity) => sum + quantity, 0));

  protected readonly cartLines = computed(() => {
    const cart = this.cart();

    return Object.entries(cart)
      .filter(([, quantity]) => quantity > 0)
      .map(([partId, quantity]) => {
        const part = this.parts().find(p => p.partId === partId)!;

        return { part, quantity, lineTotal: part.price * quantity };
      });
  });

  protected readonly cartSubtotal = computed(
    () => this.cartLines().reduce((sum, line) => sum + line.lineTotal, 0));

  protected readonly cartTax = computed(
    () => Math.round(this.cartSubtotal() * 0.15 * 100) / 100);

  protected readonly cartTotal = computed(
    () => Math.round((this.cartSubtotal() + this.cartTax()) * 100) / 100);

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

    this.http.get<PartRow[]>('/api/portal/parts').subscribe({
      next: parts => {
        this.parts.set(parts);
        this.http.get<PartOrderRow[]>('/api/portal/shop/orders').subscribe({
          next: orders => {
            this.myOrders.set(orders);
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

  protected pickCategory(label: string): void {
    this.selectedCategory.set(label);
  }

  protected addToCart(part: PartRow): void {
    if (part.stockState === 2) {
      return;
    }

    const next = { ...this.cart(), [part.partId]: (this.cart()[part.partId] ?? 0) + 1 };

    this.cart.set(next);
  }

  protected stepCart(partId: string, delta: number): void {
    const current = this.cart()[partId] ?? 0;
    const quantity = Math.max(0, current + delta);
    const next = { ...this.cart() };

    if (quantity === 0) {
      delete next[partId];
    } else {
      next[partId] = quantity;
    }

    this.cart.set(next);
  }

  protected checkout(): void {
    const lines = this.cartLines();

    if (lines.length === 0) {
      return;
    }

    this.placing.set(true);
    this.alert.set(null);

    this.http.post<PartOrderRow>('/api/portal/shop/orders', {
      items: lines.map(line => ({ partId: line.part.partId, quantity: line.quantity })),
      method: 1,
    }).subscribe({
      next: order => {
        this.placing.set(false);
        this.cart.set({});
        this.alertKind.set('ok');
        this.alert.set(`Order #${order.orderNumber} reserved. Pay in cash when you collect your parts.`);
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.placing.set(false);
        this.alertKind.set('err');
        this.alert.set(problemMessage(error) ?? 'The order could not be placed.');
        this.load();
      },
    });
  }
}
