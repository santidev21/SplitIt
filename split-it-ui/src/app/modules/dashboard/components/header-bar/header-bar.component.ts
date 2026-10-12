import { Component, OnInit, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../../auth/services/auth.service';
import { LanguageService } from '../../../../shared/services/language.service';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-header-bar',
  imports: [RouterModule, MatIconModule, TranslatePipe],
  templateUrl: './header-bar.component.html',
  styleUrls: ['./header-bar.component.scss'],
})
export class HeaderBarComponent implements OnInit {
  private authService = inject(AuthService);
  languageService = inject(LanguageService);

  isAdmin = false;
  isDark = false;
  menuOpen = false;

  constructor() {
    this.isAdmin = this.authService.isAdminRole();
  }

  ngOnInit(): void {
    this.isDark = document.documentElement.getAttribute('data-theme') === 'dark';
  }

  toggleTheme(): void {
    this.isDark = !this.isDark;
    document.documentElement.setAttribute('data-theme', this.isDark ? 'dark' : 'light');
    localStorage.setItem('theme', this.isDark ? 'dark' : 'light');
  }

  toggleLanguage(): void {
    this.languageService.toggleLanguage();
  }

  logout(): void {
    this.authService.logout();
  }
}
