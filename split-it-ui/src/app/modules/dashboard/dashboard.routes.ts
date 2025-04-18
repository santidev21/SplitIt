import { Routes } from '@angular/router';
import { DashboardComponent } from './dashboard.component';
import { GroupDetailComponent } from './components/group-detail/group-detail.component';

export const dashboardRoutes: Routes = [
  {
    path: 'home',
    component: DashboardComponent,
  },
  {
    path: 'group/:id',
    component: GroupDetailComponent,
  },
  {
    path: '',
    redirectTo: 'home',
    pathMatch: 'full',
  }
];