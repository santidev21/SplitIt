import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { authRoutes } from './auth.routes';

@NgModule({
  declarations: [],
  imports: [CommonModule, RouterModule.forChild(authRoutes)],
})
export class AuthModule {}
