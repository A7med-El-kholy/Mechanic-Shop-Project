import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthStore } from './auth-store';

/** Two areas, two doors: staff URLs need a staff token, portal URLs need a
 * customer token. Missing auth goes to /login with a returnUrl; wrong-area
 * goes to the user's own home (a Customer hitting /dashboard lands in the
 * portal, staff hitting /portal land on the dashboard). */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthStore);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  }

  return auth.hasStaffRole() ? true : router.createUrlTree(['/portal']);
};

export const portalGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthStore);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  }

  return auth.hasStaffRole() ? router.createUrlTree(['/dashboard']) : true;
};
