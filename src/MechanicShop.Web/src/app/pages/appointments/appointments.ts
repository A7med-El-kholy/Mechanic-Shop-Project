import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { problemMessage } from '../../core/api';
import { fmtDate, fmtMinute, workOrderRef } from '../../core/format';
import {
  EmployeeSummary,
  ScheduleJob,
  workOrderStateLabel,
} from '../../core/types';

/** The shop clock the board is drawn on: 08:00–20:00, 720 minutes. */
const SHOP_OPEN_MINUTE = 480;
const SHOP_CLOSE_MINUTE = 1200;
const SHOP_MINUTES = SHOP_CLOSE_MINUTE - SHOP_OPEN_MINUTE;

/** Two-hour gridlines, matching the prototype's hs = [8, 10, …, 20]. */
const HOUR_MARKS = [8, 10, 12, 14, 16, 18, 20];

function todayIso(): string {
  const now = new Date();

  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
}

/** Appointments is the day board: one lane per technician, blocks placed by the
 * same minute numbers the API stores. A block is a link into the work order —
 * the board is a read model, so nothing here mutates a schedule. */
@Component({
  selector: 'app-appointments',
  imports: [RouterLink, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './appointments.html',
  styleUrl: './appointments.scss',
})
export class AppointmentsPage {
  private readonly http = inject(HttpClient);

  protected readonly technicians = signal<EmployeeSummary[]>([]);
  protected readonly jobs = signal<ScheduleJob[]>([]);
  protected readonly date = signal(todayIso());
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);

  protected readonly fmtDate = fmtDate;
  protected readonly fmtMinute = fmtMinute;
  protected readonly ref = workOrderRef;
  protected readonly stateLabel = workOrderStateLabel;

  protected readonly hours = HOUR_MARKS;

  protected readonly dayLabel = computed(() => fmtDate(this.date()));

  protected readonly lanes = computed(() => {
    const jobs = this.jobs();

    return this.technicians().map(technician => ({
      technicianId: technician.employeeId,
      name: technician.fullName,
      jobs: jobs.filter(job => job.technicianId === technician.employeeId),
    }));
  });

  protected readonly scheduledCount = computed(() => this.jobs().length);

  ngOnInit(): void {
    this.http.get<EmployeeSummary[]>('/api/employees/technicians').subscribe({
      next: technicians => this.technicians.set(technicians),
      error: (error: HttpErrorResponse) => {
        this.pageError.set(problemMessage(error));
        this.loading.set(false);
      },
    });

    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.pageError.set(null);

    this.http
      .get<ScheduleJob[]>(
        `/api/work-orders/schedule?date=${encodeURIComponent(this.date())}`)
      .subscribe({
        next: jobs => {
          this.jobs.set(jobs);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.pageError.set(problemMessage(error));
          this.loading.set(false);
        },
      });
  }

  protected onDateChange(value: string): void {
    if (!value) {
      return;
    }

    this.date.set(value);
    this.load();
  }

  protected shiftDay(days: number): void {
    const current = new Date(`${this.date()}T00:00:00Z`);
    current.setUTCDate(current.getUTCDate() + days);
    this.date.set(current.toISOString().slice(0, 10));
    this.load();
  }

  protected goToday(): void {
    this.date.set(todayIso());
    this.load();
  }

  /** Percent of the day where the block starts. */
  protected left(job: ScheduleJob): number {
    return Math.max(0, ((job.startsAtMinute - SHOP_OPEN_MINUTE) / SHOP_MINUTES) * 100);
  }

  /** Percent width, clipped at the closing edge so an overrun never spills out. */
  protected width(job: ScheduleJob): number {
    const span = ((job.endsAtMinute - job.startsAtMinute) / SHOP_MINUTES) * 100;

    return Math.min(span, 100 - this.left(job));
  }
}
