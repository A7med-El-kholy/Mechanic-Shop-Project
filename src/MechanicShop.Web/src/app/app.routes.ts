import { Routes } from '@angular/router';

import { authGuard } from './core/auth.guard';
import { DashboardPage } from './pages/dashboard/dashboard';
import { LoginPage } from './pages/login/login';
import { NotYetPage } from './pages/not-yet/not-yet';
import { ShellPage } from './pages/shell/shell';

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
      // Screens landing in coming stages land here instead of dead 404s; each
      // tab below gets a real page as its stage completes.
      { path: '**', component: NotYetPage, title: 'Coming up · Mechanic Shop' },
    ],
  },
  { path: '**', redirectTo: '' },
];
