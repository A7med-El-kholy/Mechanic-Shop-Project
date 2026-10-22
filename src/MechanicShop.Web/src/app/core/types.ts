/** Contracts mirrored from the API — kept hand-written and narrow on purpose:
 * the SPA binds only the fields the UI needs, so backend DTO growth never leaks in. */

export interface AppUser {
  id: string;
  email: string;
  fullName: string;
  roles: string[];
}

export interface AuthResponse {
  accessToken: string;
  tokenType: string;
  expiresInSeconds: number;
  user: AppUser;
}

/** Envelope of every paged list endpoint. */
export interface PaginatedList<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface WorkOrderSummary {
  workOrderId: string;
  vehicleInfo: string;
  licensePlate: string;
  customerName: string;
  state: number;
  scheduledDate: string;
  startsAtMinute: number;
  endsAtMinute: number;
  spot: number;
  technicianName: string;
}

export interface Vehicle {
  vehicleId: string;
  make: string;
  model: string;
  year: number;
  licensePlate: string;
  vin: string;
  vehicleInfo: string;
  isArchived: boolean;
}

export interface Customer {
  customerId: string;
  name: string;
  email: string;
  phoneNumber: string;
  /** List/detail responses carry ACTIVE vehicles only; archived ones are invisible. */
  vehicles: Vehicle[];
}

/** Row of the Vehicles screen: the car plus who owns it (GET /api/vehicles). */
export interface VehicleListItem {
  vehicleId: string;
  make: string;
  model: string;
  year: number;
  licensePlate: string;
  vin: string;
  isArchived: boolean;
  customerId: string;
  customerName: string;
}

export interface EmployeeSummary {
  employeeId: string;
  fullName: string;
  role: number;
}

/** WorkOrderState as the API exposes it (ints), with the UI vocabulary. */
export const WORK_ORDER_STATES = [
  { value: 0, label: 'Scheduled' },
  { value: 1, label: 'In Progress' },
  { value: 2, label: 'Completed' },
  { value: 3, label: 'Cancelled' },
] as const;

export function workOrderStateLabel(state: number): string {
  return WORK_ORDER_STATES.find(s => s.value === state)?.label ?? `Unknown (${state})`;
}
