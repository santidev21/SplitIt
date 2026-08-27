import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { NotificationService } from '../shared/services/notification.service';

export function extractBackendMessage(err: HttpErrorResponse): string {
  const body = err.error;
  if (body) {
    if (typeof body === 'string') {
      try {
        const parsed = JSON.parse(body);
        return parsed.message || parsed.detail || parsed.error || null;
      } catch {
        return body;
      }
    }
    if (body.message) return body.message;
    if (body.detail) return body.detail;
    if (body.error && typeof body.error === 'string') return body.error;
    if (body.errors) {
      const first = Object.values(body.errors as Record<string, string[]>)[0];
      if (first && first.length > 0) return first[0];
    }
  }
  switch (err.status) {
    case 0: return 'Cannot connect to the server. Check your connection.';
    case 400: return 'Invalid request.';
    case 401: return 'Incorrect email or password.';
    case 403: return 'You do not have permission to do this.';
    case 404: return 'Resource not found.';
    case 429: return 'Too many requests. Please try again later.';
    default: return 'An unexpected error occurred. Please try again.';
  }
}

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notifications = inject(NotificationService);
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // 401 is handled by authInterceptor (session expiry redirect).
      // Business errors are surfaced to the user here.
      if (error.status !== 401) {
        const message = extractBackendMessage(error);
        notifications.toast(message, 'error');
      }
      return throwError(() => error);
    })
  );
};
