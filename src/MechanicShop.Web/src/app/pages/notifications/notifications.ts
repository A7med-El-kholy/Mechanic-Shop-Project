import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

import { problemMessage } from '../../core/api';
import { fmtRelative } from '../../core/format';
import { NotificationBadgeService } from '../../core/notifications';
import {
  NotificationRow,
  PaginatedList,
  notificationCategoryLabel,
} from '../../core/types';

type Filter = 'all' | 'unread';

/** The prototype's single nt() view: one list for staff and customers, scoped
 * by the API. The `portal` flag comes from route data — the UI is shared, the
 * inbox is not. A row with a work order opens that ticket on the reader's own
 * side: portal repairs or the staff work-order screen. */
@Component({
  selector: 'app-notifications',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './notifications.html',
  styleUrl: './notifications.scss',
})
export class NotificationsPage {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly badge = inject(NotificationBadgeService);

  protected readonly portal = this.route.snapshot.data['portal'] === true;

  private readonly basePath = this.portal
    ? '/api/portal/notifications'
    : '/api/notifications';

  protected readonly rows = signal<NotificationRow[]>([]);
  protected readonly filter = signal<Filter>('all');
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly pageError = signal<string | null>(null);

  protected readonly categoryLabel = notificationCategoryLabel;
  protected readonly fmtRelative = fmtRelative;

  ngOnInit(): void {
    this.load();
  }

  protected setFilter(filter: Filter): void {
    if (this.filter() === filter) {
      return;
    }

    this.filter.set(filter);
    this.load();
  }

  protected markAllRead(): void {
    this.busy.set(true);
    this.pageError.set(null);

    this.http.post<void>(`${this.basePath}/read-all`, {}).subscribe({
      next: () => {
        this.busy.set(false);
        this.badge.refresh(this.portal ? 'portal' : 'staff');
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.pageError.set(problemMessage(error));
      },
    });
  }

  protected setRead(row: NotificationRow, isRead: boolean): void {
    this.busy.set(true);
    this.pageError.set(null);

    this.http
      .put<void>(`${this.basePath}/${row.notificationId}/read-state`, { isRead })
      .subscribe({
        next: () => {
          this.busy.set(false);
          // The server owns the state; mirror it locally instead of reloading.
          this.rows.update(rows =>
            rows.map(r =>
              r.notificationId === row.notificationId
                ? { ...r, isRead, readOn: isRead ? new Date().toISOString() : null }
                : r));
          this.badge.refresh(this.portal ? 'portal' : 'staff');
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.pageError.set(problemMessage(error));
        },
      });
  }

  protected open(row: NotificationRow): void {
    if (!row.workOrderId) {
      return;
    }

    void this.router.navigate(
      [this.portal ? '/portal/repairs' : '/work-orders', row.workOrderId]);
  }

  private load(): void {
    this.loading.set(true);
    this.pageError.set(null);

    const unreadOnly = this.filter() === 'unread';

    this.http
      .get<PaginatedList<NotificationRow>>(
        `${this.basePath}?unreadOnly=${unreadOnly}&page=1&pageSize=50`)
      .subscribe({
        next: page => {
          this.rows.set(page.items);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.pageError.set(problemMessage(error));
          this.loading.set(false);
        },
      });
  }
}
