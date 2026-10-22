import { Routes } from '@angular/router';

import { authGuard } from './core/auth.guard';
import { CustomerDetailPage } from './pages/customer-detail/customer-detail';
import { CustomersPage } from './pages/customers/customers';
import { DashboardPage } from './pages/dashboard/dashboard';
import { LoginPage } from './pages/login/login';
import { NotYetPage } from './pages/not-yet/not-yet';
import { ShellPage } from './pages/shell/shell';
import { VehiclesPage } from './pages/vehicles/vehicles';

export const routes: Routes = [
  {
    path: 'login',
    component: LoginPage,
    title: 'Sign in · Mechanic Shop',
  },
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
      // Screens landing in coming stages land here instead of dead 404s; each
      // tab below gets a real page as its stage completes.
      { path: '**', component: NotYetPage, title: 'Coming up · Mechanic Shop' },
    ],
  },
  { path: '**', redirectTo: '' },
];
