import { Routes } from '@angular/router';

export const routes: Routes = [
    {
      path: 'auth',
      loadChildren: () => import('./modules/auth/auth.module').then(m => m.AuthModule)
    },
    {
      path: 'dashboard',
      loadChildren: () => import('./modules/dashboard/dashboard.module').then(m => m.DashboardModule)
    },
    {
      path: 'admin',
      loadChildren: () => import('./modules/admin/admin.module').then(m => m.AdminModule)
    },
    { path: '', redirectTo: 'auth/login', pathMatch: 'full' }
  ];