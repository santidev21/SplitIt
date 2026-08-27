import { Component } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../../auth/services/auth.service';

@Component({
  selector: 'app-header-bar',
  imports: [RouterModule],
  templateUrl: './header-bar.component.html',
  styleUrls: ['./header-bar.component.scss']
})
export class HeaderBarComponent {
  isAdmin = false;

  constructor(
    private router: Router,
    private authService: AuthService
  ){
    this.isAdmin = this.readRole() === 'SuperAdmin' || this.readRole() === 'Admin';
  }

  private readRole(): string | null {
    const token = localStorage.getItem('token');
    if (!token) return null;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || payload.role || null;
    } catch {
      return null;
    }
  }

  logout(){
    this.authService.logout();
  }
}
