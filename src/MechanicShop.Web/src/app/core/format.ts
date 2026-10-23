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

/** The API identifies a work order by GUID; the shop floor calls it WO-XXXXXXXX.
 * Derived, never stored — the id stays the only identity. */
export function workOrderRef(workOrderId: string): string {
  return `WO-${workOrderId.replace(/-/g, '').slice(0, 8).toUpperCase()}`;
}

/** ISO timestamp → "just now" / "5m ago" / "3h ago" / "Yesterday" / a date.
 * The prototype seeded human strings ("2 min ago"); real rows carry the real
 * timestamp, so the label is derived at render time. */
export function fmtRelative(iso: string): string {
  const then = new Date(iso).getTime();

  if (!Number.isFinite(then)) {
    return iso;
  }

  const seconds = Math.max(0, Math.floor((Date.now() - then) / 1000));

  if (seconds < 60) {
    return 'just now';
  }

  const minutes = Math.floor(seconds / 60);

  if (minutes < 60) {
    return `${minutes}m ago`;
  }

  const hours = Math.floor(minutes / 60);

  if (hours < 24) {
    return `${hours}h ago`;
  }

  if (hours < 48) {
    return 'Yesterday';
  }

  return new Date(then).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}
