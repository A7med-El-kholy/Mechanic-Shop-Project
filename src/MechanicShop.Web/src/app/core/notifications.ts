import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';

/** The bell badge shared by both shells: one scoped COUNT, never rows. */
@Injectable({ providedIn: 'root' })
export class NotificationBadgeService {
  private readonly http = inject(HttpClient);

  readonly unread = signal(0);

  refresh(scope: 'staff' | 'portal'): void {
    const url = scope === 'portal'
      ? '/api/portal/notifications/unread-count'
      : '/api/notifications/unread-count';

    this.http.get<{ count: number }>(url).subscribe({
      next: response => this.unread.set(response.count),
      // A badge is decoration: if the count cannot load, never block the page.
      error: () => this.unread.set(0),
    });
  }
}
