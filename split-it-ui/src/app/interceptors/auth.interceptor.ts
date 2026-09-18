import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError, Observable, shareReplay, finalize } from 'rxjs';
import { AuthService } from '../modules/auth/services/auth.service';

// Single in-flight refresh shared by all concurrent 401s, so we never fire
// several refresh calls at once (which would race and log the user out).
let refreshInFlight: Observable<{ token: string } | null> | null = null;

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);

  if (req.url.includes('/auth/refresh') || req.url.includes('/auth/logout')) {
    return next(req);
  }

  const token = authService.getToken();
  const authReq = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== 401) {
        return throwError(() => error);
      }

      if (!refreshInFlight) {
        refreshInFlight = authService.refreshSession().pipe(
          finalize(() => { refreshInFlight = null; }),
          shareReplay({ bufferSize: 1, refCount: false })
        );
      }

      return refreshInFlight.pipe(
        switchMap((result) => {
          if (!result) {
            authService.logout();
            return throwError(() => error);
          }
          const newToken = authService.getToken();
          const retryReq = newToken ? req.clone({ setHeaders: { Authorization: `Bearer ${newToken}` } }) : req;
          return next(retryReq);
        })
      );
    })
  );
};
