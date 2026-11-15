import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, catchError, finalize, throwError } from 'rxjs';

import { AuthStore } from './auth-store';
import { useLoading } from './loading';

/**
 * HTTP interceptor that:
 *  1. Attaches the bearer token from AuthStore
 *  2. Tracks pending requests via the global loading signal
 *  3. Handles 401 by clearing session and redirecting to login
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthStore);
  const { start, stop } = useLoading();

  const token = auth.isAuthenticated() ? auth.token() : null;
  const authed = token === null
    ? req
    : req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

  start();

  return next(authed).pipe(
    finalize(() => stop()),
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        auth.logout();
      }
      return throwError(() => error);
    }),
  );
};
