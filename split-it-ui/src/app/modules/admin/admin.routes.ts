import { Routes } from '@angular/router';
import { AdminPageComponent } from './components/admin-page/admin-page.component';
import { authGuard } from '../auth/guards/auth.guard';
import { adminGuard } from '../auth/guards/admin.guard';

export const adminRoutes: Routes = [
  {
    path: '',
    component: AdminPageComponent,
    canActivate: [authGuard, adminGuard]
  }
];
