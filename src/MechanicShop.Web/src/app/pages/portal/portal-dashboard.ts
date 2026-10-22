import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';

import { Customer, WorkOrderSummary, workOrderStateLabel } from '../../core/types';
import { fmtDate, fmtMinute } from '../../core/format';
import { problemMessage } from '../../core/api';

const FEATURED_STATE = 1; // In Progress gets the spotlight, like the prototype.

/** The customer's landing page: what the shop is doing for them right now and
 * what they own. Invoices and the AI assistant follow in later stages. */
@Component({
  selector: 'app-portal-dashboard',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './portal-dashboard.html',
  styleUrl: './portal-dashboard.scss',
})
export class PortalDashboardPage {
  private readonly http = inject(HttpClient);

  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);
  protected readonly profile = signal<Customer | null>(null);
  protected readonly repairs = signal<WorkOrderSummary[]>([]);

  protected readonly fmtDate = fmtDate;
  protected readonly fmtMinute = fmtMinute;
  protected readonly stateLabel = workOrderStateLabel;

  protected readonly firstName = computed(() => {
    const profile = this.profile();

    if (!profile) {
      return '';
    }

    return profile.name.split(/\s+/)[0] ?? profile.name;
  });

  protected readonly activeCount = computed(
    () => this.repairs().filter(r => r.state === FEATURED_STATE).length);

  protected readonly upcomingCount = computed(
    () => this.repairs().filter(r => r.state === 0).length);

  /** An in-progress job in the bay, else the soonest scheduled visit. */
  protected readonly spotlight = computed<WorkOrderSummary | null>(() => {
    const repairs = this.repairs();
    const active = repairs.find(r => r.state === FEATURED_STATE);

    if (active) {
      return active;
    }

    return repairs
      .filter(r => r.state === 0)
      .sort((a, b) =>
        a.scheduledDate.localeCompare(b.scheduledDate) || a.startsAtMinute - b.startsAtMinute)[0]
      ?? null;
  });

  protected readonly spotlightTitle = computed(() =>
    this.spotlight()?.state === FEATURED_STATE ? 'Active repair' : 'Next visit');

  ngOnInit(): void {
    this.http
      .get<Customer>('/api/customers/me')
      .subscribe({
        next: profile => {
          this.profile.set(profile);
          this.loadRepairs();
        },
        error: (error: HttpErrorResponse) => {
          // The deliberate workshop-first design: a signed-up customer without a
          // profile is a real visitor state, not a crash.
          this.pageError.set(problemMessage(error));
          this.loading.set(false);
        },
      });
  }

  private loadRepairs(): void {
    this.http
      .get<{ items: WorkOrderSummary[]; totalCount: number }>('/api/portal/repairs?page=1&pageSize=50')
      .subscribe({
        next: page => {
          this.repairs.set(page.items);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.pageError.set(problemMessage(error));
          this.loading.set(false);
        },
      });
  }
}
