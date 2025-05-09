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

  constructor(
    private router: Router,
    private authService: AuthService
  ){}

  logout(){
    this.authService.logout();  
  }
}
