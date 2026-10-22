import { Routes } from '@angular/router';

import { authGuard, portalGuard } from './core/auth.guard';
import { CustomerDetailPage } from './pages/customer-detail/customer-detail';
import { CustomersPage } from './pages/customers/customers';
import { DashboardPage } from './pages/dashboard/dashboard';
import { InvoiceDetailPage } from './pages/invoices/invoice-detail';
import { InvoicesPage } from './pages/invoices/invoices';
import { LoginPage } from './pages/login/login';
import { NotYetPage } from './pages/not-yet/not-yet';
import { ShellPage } from './pages/shell/shell';
import { VehiclesPage } from './pages/vehicles/vehicles';
import { PortalShellPage } from './pages/portal/portal-shell';
import { PortalAppointmentsPage } from './pages/portal/portal-appointments';
import { PortalDashboardPage } from './pages/portal/portal-dashboard';
import { PortalInvoiceDetailPage } from './pages/portal/portal-invoice-detail';
import { PortalInvoicesPage } from './pages/portal/portal-invoices';
import { PortalRepairDetailPage } from './pages/portal/portal-repair-detail';
import { PortalRepairsPage } from './pages/portal/portal-repairs';
import { PortalVehiclesPage } from './pages/portal/portal-vehicles';

export const routes: Routes = [
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
        path: 'appointments',
        component: PortalAppointmentsPage,
        title: 'Appointments · Mechanic Shop',
      },
    ],
  },
  // The staff side.
  {
    path: '',
    component: ShellPage,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', component: DashboardPage, title: 'Dashboard · Mechanic Shop' },
      { path: 'customers', component: CustomersPage, title: 'Customers · Mechanic Shop' },
      {
        path: 'customers/:customerId',
        component: CustomerDetailPage,
        title: 'Customer · Mechanic Shop',
      },
      { path: 'vehicles', component: VehiclesPage, title: 'Vehicles · Mechanic Shop' },
      { path: 'invoices', component: InvoicesPage, title: 'Invoices · Mechanic Shop' },
      {
        path: 'invoices/:invoiceId',
        component: InvoiceDetailPage,
        title: 'Invoice · Mechanic Shop',
      },
      // Screens landing in coming stages land here instead of dead 404s; each
      // tab below gets a real page as its stage completes.
      { path: '**', component: NotYetPage, title: 'Coming up · Mechanic Shop' },
    ],
  },
  { path: '**', redirectTo: '' },
];

