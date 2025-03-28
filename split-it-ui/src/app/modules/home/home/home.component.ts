import { Component } from '@angular/core';
import { HeaderBarComponent } from '../components/header-bar/header-bar.component';

@Component({
  selector: 'app-home',
  imports: [HeaderBarComponent],
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss']
})
export class HomeComponent {
  userName: string | null = localStorage.getItem('userName');
}
