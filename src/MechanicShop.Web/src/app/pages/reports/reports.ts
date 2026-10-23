import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';

import { ReportSummary } from '../../core/types';
import { problemMessage } from '../../core/api';

/** The manager's Reports page: lifecycle KPIs, the money picture, and the two
 * breakdown bars — estimated value by service and minutes booked per
 * technician. Everything is computed by the API on read; this page only
 * renders. Null metrics (no data yet) show an em dash, never a fake zero. */
@Component({
  selector: 'app-reports',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reports.html',
  styleUrl: './reports.scss',
})
export class ReportsPage {
  private readonly http = inject(HttpClient);

  protected readonly summary = signal<ReportSummary | null>(null);
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);

  protected readonly money = new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  });

  /** Bar scales: services relative to the biggest earner, minutes on the
   * prototype's 300-minute full scale, floored so an empty chart still
   * divides safely. */
  protected readonly maxValue = computed(
    () => Math.max(...(this.summary()?.valueByService.map(r => r.estimatedValue) ?? [0]), 1));

  protected readonly maxMinutes = computed(
    () => Math.max(...(this.summary()?.technicianWorkload.map(r => r.bookedMinutes) ?? [0]), 300));

  protected serviceWidth(value: number): string {
    return `${(value / this.maxValue()) * 100}%`;
  }

  protected workloadWidth(minutes: number): string {
    return `${(minutes / this.maxMinutes()) * 100}%`;
  }

  ngOnInit(): void {
    this.http.get<ReportSummary>('/api/reports/summary').subscribe({
      next: summary => {
        this.summary.set(summary);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.pageError.set(problemMessage(error));
        this.loading.set(false);
      },
    });
  }
}
