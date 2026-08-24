import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';

function isTokenExpired(token: string): boolean {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    if (!payload.exp) return true;
    const now = Math.floor(Date.now() / 1000);
    // 30s leeway to avoid edge
    return payload.exp < now - 30;
  } catch {
    return true;
  }
}

export const authGuard: CanActivateFn = (route, state) => {
  const router = inject(Router);
  const token = localStorage.getItem('token');

  if (!token || isTokenExpired(token)) {
    localStorage.removeItem('token');
    localStorage.removeItem('userName');
    localStorage.removeItem('userId');
    router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url } });
    return false;
  }
  return true;
};
