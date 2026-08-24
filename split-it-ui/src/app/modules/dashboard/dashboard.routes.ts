import { Routes } from '@angular/router';
import { DashboardComponent } from './dashboard.component';
import { GroupDetailComponent } from './components/group-detail/group-detail.component';
import { authGuard } from '../auth/guards/auth.guard';

export const dashboardRoutes: Routes = [
  {
    path: 'home',
    component: DashboardComponent,
    canActivate: [authGuard]
  },
  {
    path: 'group/:id',
    component: GroupDetailComponent,
    canActivate: [authGuard]
  },
  {
    path: '',
    redirectTo: 'home',
    pathMatch: 'full',
  }
];