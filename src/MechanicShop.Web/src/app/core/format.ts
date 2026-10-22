/** Small display helpers shared by pages. */

/** "2027-01-15" → "Mon, Jan 15 2027" — split by hand so no timezone drifts the day. */
export function fmtDate(dateOnly: string): string {
  const [year, month, day] = dateOnly.split('-').map(Number);

  if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)) {
    return dateOnly;
  }

  return new Date(year, month - 1, day).toLocaleDateString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

/** Minute-of-day 990 → "16:30" (the prototype's f()). */
export function fmtMinute(minute: number): string {
  return `${String(Math.floor(minute / 60)).padStart(2, '0')}:${String(minute % 60).padStart(2, '0')}`;
}
