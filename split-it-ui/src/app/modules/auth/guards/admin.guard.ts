import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { isAdminRole } from '../../../shared/utils/jwt.util';

export const adminGuard: CanActivateFn = (route, state) => {
  const router = inject(Router);

  if (isAdminRole()) {
    return true;
  }
  router.navigate(['/dashboard/home']);
  return false;
};
