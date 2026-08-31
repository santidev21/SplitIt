import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { isTokenExpired } from '../../../shared/utils/jwt.util';
import { map, catchError, of } from 'rxjs';

export const authGuard: CanActivateFn = (route, state) => {
  const router = inject(Router);
  const authService = inject(AuthService);

  const token = authService.getToken();

  if (token && !isTokenExpired(token)) {
    return true;
  }

  return authService.refreshSession().pipe(
    map((result) => {
      if (result) return true;
      router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url } });
      return false;
    }),
    catchError(() => {
      router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url } });
      return of(false);
    })
  );
};
