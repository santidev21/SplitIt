import { Component } from '@angular/core';
import { HeaderBarComponent } from '../components/header-bar/header-bar.component';

@Component({
  selector: 'app-dashboard',
  imports: [HeaderBarComponent],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent {
  userName: string | null = localStorage.getItem('userName');
}
