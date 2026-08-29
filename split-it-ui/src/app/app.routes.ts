import { Routes } from '@angular/router';
import { authGuard } from './modules/auth/guards/auth.guard';

function authAwareRedirect() {
  const token = localStorage.getItem('token');
  if (!token) return '/auth/login';
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    if (payload.exp && payload.exp > Math.floor(Date.now() / 1000)) {
      return '/dashboard/home';
    }
  } catch {}
  return '/auth/login';
}

export const routes: Routes = [
    {
      path: 'auth',
      loadChildren: () => import('./modules/auth/auth.module').then(m => m.AuthModule)
    },
    {
      path: 'dashboard',
      loadChildren: () => import('./modules/dashboard/dashboard.module').then(m => m.DashboardModule)
    },
    { path: '', redirectTo: 'auth/login', pathMatch: 'full' },
    { path: '**', redirectTo: authAwareRedirect }
  ];