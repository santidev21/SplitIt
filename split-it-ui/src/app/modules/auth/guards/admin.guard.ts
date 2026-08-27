import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';

function getRoleFromToken(): string | null {
  const token = localStorage.getItem('token');
  if (!token) return null;
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    return payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || payload.role || null;
  } catch {
    return null;
  }
}

export const adminGuard: CanActivateFn = (route, state) => {
  const router = inject(Router);
  const role = getRoleFromToken();

  if (role === 'SuperAdmin' || role === 'Admin') {
    return true;
  }
  router.navigate(['/dashboard/home']);
  return false;
};
