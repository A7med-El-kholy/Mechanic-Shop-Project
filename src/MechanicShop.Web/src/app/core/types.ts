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

/** One drawable block on the appointments board (WorkOrderScheduleDto). */
export interface ScheduleJob {
  workOrderId: string;
  vehicleInfo: string;
  licensePlate: string;
  customerName: string;
  technicianId: string;
  technicianName: string;
  state: number;
  startsAtMinute: number;
  endsAtMinute: number;
  spot: number;
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

/** One appointment request row (mine + the staff review list). */
export interface AppointmentRequestRow {
  appointmentRequestId: string;
  customerId: string;
  customerName: string;
  vehicleId: string;
  vehicleInfo: string;
  licensePlate: string;
  serviceTaskId: string | null;
  serviceTaskName: string | null;
  serviceTaskPrice: number | null;
  preferredDate: string;
  status: number;
}

/** Catalog read for the request form (GET /api/portal/service-options). */
export interface ServiceOption {
  serviceTaskId: string;
  name: string;
  basePrice: number;
  estimatedDurationInMins: number;
}

/** The full WorkOrderDto — the staff detail screen and the customer's repair
 * ticket read the same document. */
export interface WorkOrderDetail extends WorkOrderSummary {
  totalPrice: number;
  estimatedDurationTotalInMins: number;
  serviceTasks: ServiceOption[];
}

/** The portal's name for the same ticket. */
export type RepairDetail = WorkOrderDetail;

/** Row of the invoices book (staff list). */
export interface InvoiceRow {
  invoiceId: string;
  invoiceNumber: string;
  workOrderId: string;
  customerName: string;
  vehicleInfo: string;
  licensePlate: string;
  status: number;
  totalAmount: number;
  issuedOn: string;
  paymentMethod: number | null;
}

/** Full invoice document: frozen lines + the money math. */
export interface InvoiceDetail extends InvoiceRow {
  subtotal: number;
  taxRate: number;
  taxAmount: number;
  lineItems: { serviceTaskId: string; serviceName: string; amount: number }[];
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

export const INVOICE_STATUSES = [
  { value: 0, label: 'Unpaid' },
  { value: 1, label: 'Paid' },
  { value: 2, label: 'Cash Pending' },
] as const;

export function invoiceStatusLabel(status: number): string {
  return INVOICE_STATUSES.find(s => s.value === status)?.label ?? `Unknown (${status})`;
}

export const PART_CATEGORIES = [
  { value: 0, label: 'Engine' },
  { value: 1, label: 'Brakes' },
  { value: 2, label: 'Electrical' },
  { value: 3, label: 'Fluids' },
] as const;

export function partCategoryLabel(category: number): string {
  return PART_CATEGORIES.find(c => c.value === category)?.label ?? `Unknown (${category})`;
}

export const PART_STOCK_STATES = [
  { value: 0, label: 'In stock' },
  { value: 1, label: 'Low stock' },
  { value: 2, label: 'Out of stock' },
] as const;

export function partStockLabel(state: number): string {
  return PART_STOCK_STATES.find(s => s.value === state)?.label ?? `Unknown (${state})`;
}

/** One shelf row — the storeroom table and the customer shop share it. */
export interface PartRow {
  partId: string;
  name: string;
  category: number;
  price: number;
  quantity: number;
  minimumQuantity: number;
  stockState: number;
}

/** One purchased line: name/price as charged (snapshots), quantity. */
export interface PartOrderLineRow {
  partId: string;
  partName: string;
  quantity: number;
  unitPrice: number;
}

/** The parts receipt, both customer and manager views. */
export interface PartOrderRow {
  orderId: string;
  orderNumber: string;
  customerId: string;
  customerName: string;
  placedOn: string;
  status: number;
  paymentMethod: number | null;
  subtotal: number;
  taxRate: number;
  taxAmount: number;
  totalAmount: number;
  lines: PartOrderLineRow[];
}

/** One service's estimated value across non-cancelled work orders. */
export interface ReportServiceRow {
  serviceName: string;
  estimatedValue: number;
}

/** One technician's booked minutes (idle techs appear with zero). */
export interface ReportTechRow {
  technicianName: string;
  bookedMinutes: number;
}

/** The manager's operational summary — every figure derived on read. */
export interface ReportSummary {
  workOrderTotal: number;
  workOrderCompleted: number;
  completionRatePercent: number | null;
  valueBasisOrders: number;
  estimatedValueSum: number;
  averageOrderValue: number | null;
  invoicesTotal: number;
  invoicesPaid: number;
  valueByService: ReportServiceRow[];
  technicianWorkload: ReportTechRow[];
}

/** One inbox row — staff and customer copies share this shape; the API scopes. */
export interface NotificationRow {
  notificationId: string;
  audience: number;
  title: string;
  message: string;
  category: number;
  workOrderId: string | null;
  isRead: boolean;
  createdOn: string;
  readOn: string | null;
}

/** Category badges (the prototype's cat): only categories real events produce. */
export const NOTIFICATION_CATEGORIES = [
  { value: 0, label: 'Repairs' },
  { value: 1, label: 'Billing' },
  { value: 2, label: 'Inventory' },
  { value: 3, label: 'Orders' },
  { value: 4, label: 'Requests' },
  { value: 5, label: 'Assignments' },
] as const;

export function notificationCategoryLabel(category: number): string {
  return NOTIFICATION_CATEGORIES.find(c => c.value === category)?.label ?? `Unknown (${category})`;
}
