import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { EmployeeSummary, ScheduleJob } from './types';

/** The two day reads every schedule view needs: who is on the roster, and what
 * that day holds. Both the appointments board and the technicians roster are
 * views of the same two answers — the join happens in the page, not as a
 * duplicate API aggregate. */
@Injectable({ providedIn: 'root' })
export class ScheduleService {
  private readonly http = inject(HttpClient);

  technicians(): Observable<EmployeeSummary[]> {
    return this.http.get<EmployeeSummary[]>('/api/employees/technicians');
  }

  jobsForDate(date: string): Observable<ScheduleJob[]> {
    return this.http.get<ScheduleJob[]>(
      `/api/work-orders/schedule?date=${encodeURIComponent(date)}`);
  }
}
