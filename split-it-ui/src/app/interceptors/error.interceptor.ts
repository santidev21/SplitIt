import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import Swal from 'sweetalert2';

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
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== 401) {
        const message = extractBackendMessage(error);
        Swal.fire({
          toast: true,
          position: 'center',
          icon: 'error',
          title: message,
          showConfirmButton: true,
          confirmButtonText: 'OK',
          confirmButtonColor: '#005cbb',
          timer: undefined,
          timerProgressBar: false,
          customClass: { popup: 'centered-toast' }
        });
      }
      return throwError(() => error);
    })
  );
};
