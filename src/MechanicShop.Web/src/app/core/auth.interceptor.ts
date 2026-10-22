import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { AuthStore } from './auth-store';

/**
 * Attaches the bearer token to API calls and treats a hard 401 as "session
 * ended": clears local state and re-runs the sign-in flow, remembering where
 * the user was so a refresh can resume it.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthStore);
  const router = inject(Router);

  const token = auth.isAuthenticated() ? auth.token() : null;

  const authed = token === null
    ? req
    : req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

  return next(authed).pipe(catchError((error: unknown) => {
    if (error instanceof HttpErrorResponse && error.status === 401) {
      auth.logout();
      void router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
    }

    return throwError(() => error);
  }));
};
