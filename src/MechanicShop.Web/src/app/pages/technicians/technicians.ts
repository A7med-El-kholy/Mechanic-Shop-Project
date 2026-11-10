import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { problemMessage } from '../../core/api';
import {
  fmtDate,
  fmtMinute,
  shiftIsoDate,
  todayIso,
  workOrderRef,
} from '../../core/format';
import { ScheduleService } from '../../core/schedule';
import {
  ScheduleJob,
  workOrderStateLabel,
} from '../../core/types';

/** One card on the roster's day book. */
export interface TechnicianCard {
  technicianId: string;
  name: string;
  jobs: ScheduleJob[];
  /** "Working now": at least one job later than Scheduled. */
  busy: boolean;
  bookedMinutes: number;
}

/** The roster as a day book: who is on the floor, who is under a car right now,
 * and what the day holds for each pair of hands. It is a join of the two day
 * reads the board already answers — no third API aggregate to keep in sync. */
@Component({
  selector: 'app-technicians',
  imports: [RouterLink, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './technicians.html',
  styleUrl: './technicians.scss',
})
export class TechniciansPage {
  private readonly schedule = inject(ScheduleService);

  protected readonly date = signal(todayIso());
  protected readonly cards = signal<TechnicianCard[]>([]);
  protected readonly loading = signal(true);
  protected readonly pageError = signal<string | null>(null);

  protected readonly fmtDate = fmtDate;
  protected readonly fmtMinute = fmtMinute;
  protected readonly ref = workOrderRef;
  protected readonly stateLabel = workOrderStateLabel;

  protected readonly dayLabel = computed(() => fmtDate(this.date()));

  protected readonly workingNow = computed(
    () => this.cards().filter(card => card.busy).length);

  protected readonly bookedCount = computed(
    () => this.cards().reduce((sum, card) => sum + card.jobs.length, 0));

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.pageError.set(null);

    forkJoin({
      technicians: this.schedule.technicians(),
      jobs: this.schedule.jobsForDate(this.date()),
    }).subscribe({
      next: ({ technicians, jobs }) => {
        const byTechnician = new Map<string, ScheduleJob[]>();

        for (const job of jobs) {
          const mine = byTechnician.get(job.technicianId) ?? [];
          mine.push(job);
          byTechnician.set(job.technicianId, mine);
        }

        this.cards.set(
          technicians.map(technician => {
            const mine = byTechnician.get(technician.employeeId) ?? [];

            return {
              technicianId: technician.employeeId,
              name: technician.fullName,
              jobs: mine,
              busy: mine.some(job => job.state === 1),
              bookedMinutes: mine.reduce(
                (sum, job) => sum + (job.endsAtMinute - job.startsAtMinute),
                0),
            };
          }));

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
    this.date.set(shiftIsoDate(this.date(), days));
    this.load();
  }

  protected goToday(): void {
    this.date.set(todayIso());
    this.load();
  }
}
