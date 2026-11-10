import { Routes } from '@angular/router';

import { authGuard, managerGuard, portalGuard } from './core/auth.guard';
import { CustomerDetailPage } from './pages/customer-detail/customer-detail';
import { AppointmentsPage } from './pages/appointments/appointments';
import { CustomersPage } from './pages/customers/customers';
import { TechniciansPage } from './pages/technicians/technicians';
import { DashboardPage } from './pages/dashboard/dashboard';
import { InvoiceDetailPage } from './pages/invoices/invoice-detail';
import { InvoicesPage } from './pages/invoices/invoices';
import { InventoryPage } from './pages/inventory/inventory';
import { LoginPage } from './pages/login/login';
import { NotYetPage } from './pages/not-yet/not-yet';
import { NotificationsPage } from './pages/notifications/notifications';
import { StaffHomePage } from './pages/staff-home/staff-home';
import { PortalShopPage } from './pages/portal/shop';
import { ReportsPage } from './pages/reports/reports';
import { ShellPage } from './pages/shell/shell';
import { TechHomePage } from './pages/tech-home/tech-home';
import { VehiclesPage } from './pages/vehicles/vehicles';
import { WorkOrderDetailPage } from './pages/work-orders/work-order-detail';
import { WorkOrderNewPage } from './pages/work-orders/work-order-new';
import { WorkOrdersPage } from './pages/work-orders/work-orders';
import { PortalShellPage } from './pages/portal/portal-shell';
import { PortalAppointmentsPage } from './pages/portal/portal-appointments';
import { PortalAiPage } from './pages/portal/portal-ai';
import { PortalDashboardPage } from './pages/portal/portal-dashboard';
import { PortalInvoiceDetailPage } from './pages/portal/portal-invoice-detail';
import { PortalInvoicesPage } from './pages/portal/portal-invoices';
import { PortalRepairDetailPage } from './pages/portal/portal-repair-detail';
import { PortalRepairsPage } from './pages/portal/portal-repairs';
import { PortalVehiclesPage } from './pages/portal/portal-vehicles';
import { HomePage } from './pages/home/home';

export const routes: Routes = [
  {
    path: 'home',
    component: HomePage,
    title: 'Mechanic Shop · Professional Auto Repair',
  },
  {
    path: 'login',
    component: LoginPage,
    title: 'Sign in · Mechanic Shop',
  },
  // The customer side. Same token, different role — customers never render the
  // staff shell and staff never land here (both guards bounce wrong-area users).
  {
    path: 'portal',
    component: PortalShellPage,
    canActivate: [portalGuard],
    children: [
      { path: '', pathMatch: 'full', component: PortalDashboardPage, title: 'My Shop · Mechanic Shop' },
      { path: 'vehicles', component: PortalVehiclesPage, title: 'My Vehicles · Mechanic Shop' },
      { path: 'repairs', component: PortalRepairsPage, title: 'My Repairs · Mechanic Shop' },
      {
        path: 'repairs/:repairId',
        component: PortalRepairDetailPage,
        title: 'Repair · Mechanic Shop',
      },
      { path: 'invoices', component: PortalInvoicesPage, title: 'Invoices · Mechanic Shop' },
      {
        path: 'invoices/:invoiceId',
        component: PortalInvoiceDetailPage,
        title: 'Invoice · Mechanic Shop',
      },
      {
        path: 'shop',
        component: PortalShopPage,
        title: 'Parts Shop · Mechanic Shop',
      },
      {
        path: 'appointments',
        component: PortalAppointmentsPage,
        title: 'Appointments · Mechanic Shop',
      },
      {
        path: 'ai-assistant',
        component: PortalAiPage,
        title: 'AI Assistant · Mechanic Shop',
      },
      {
        path: 'notifications',
        component: NotificationsPage,
        data: { portal: true },
        title: 'Notifications · Mechanic Shop',
      },
    ],
  },
  // The staff side.
  {
    path: '',
    component: ShellPage,
    canActivate: [authGuard],
    children: [
      // Each role lands in its own home; the component branches on the role.
      { path: '', pathMatch: 'full', component: StaffHomePage, title: 'Mechanic Shop' },
      {
        path: 'tech-home',
        component: TechHomePage,
        title: 'My Dashboard · Mechanic Shop',
      },
      {
        path: 'dashboard',
        component: DashboardPage,
        canActivate: [managerGuard],
        title: 'Dashboard · Mechanic Shop',
      },
      {
        path: 'customers',
        component: CustomersPage,
        canActivate: [managerGuard],
        title: 'Customers · Mechanic Shop',
      },
      {
        path: 'customers/:customerId',
        component: CustomerDetailPage,
        canActivate: [managerGuard],
        title: 'Customer · Mechanic Shop',
      },
      {
        path: 'vehicles',
        component: VehiclesPage,
        canActivate: [managerGuard],
        title: 'Vehicles · Mechanic Shop',
      },
      { path: 'appointments', component: AppointmentsPage, title: 'Schedule · Mechanic Shop' },
      {
        path: 'technicians',
        component: TechniciansPage,
        canActivate: [managerGuard],
        title: 'Technicians · Mechanic Shop',
      },
      { path: 'work-orders', component: WorkOrdersPage, title: 'Work Orders · Mechanic Shop' },
      {
        path: 'work-orders/new',
        component: WorkOrderNewPage,
        canActivate: [managerGuard],
        title: 'New work order · Mechanic Shop',
      },
      {
        path: 'work-orders/:workOrderId',
        component: WorkOrderDetailPage,
        title: 'Work order · Mechanic Shop',
      },
      {
        path: 'invoices',
        component: InvoicesPage,
        canActivate: [managerGuard],
        title: 'Invoices · Mechanic Shop',
      },
      // The prototype shows "View invoice" from a technician's own finished job,
      // so detail stays staff-reachable even though the list is counter-only.
      {
        path: 'invoices/:invoiceId',
        component: InvoiceDetailPage,
        title: 'Invoice · Mechanic Shop',
      },
      {
        path: 'inventory',
        component: InventoryPage,
        canActivate: [managerGuard],
        title: 'Inventory · Mechanic Shop',
      },
      {
        path: 'reports',
        component: ReportsPage,
        canActivate: [managerGuard],
        title: 'Reports · Mechanic Shop',
      },
      {
        path: 'notifications',
        component: NotificationsPage,
        title: 'Notifications · Mechanic Shop',
      },
      // Screens landing in coming stages land here instead of dead 404s; each
      // tab below gets a real page as its stage completes.
      { path: '**', component: NotYetPage, title: 'Coming up · Mechanic Shop' },
    ],
  },
  { path: '**', redirectTo: '' },
];

