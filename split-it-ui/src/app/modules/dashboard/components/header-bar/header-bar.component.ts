import { Component, OnInit } from '@angular/core';
import { NgIf } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../../auth/services/auth.service';
import { LanguageService } from '../../../../shared/services/language.service';
import { MatIconModule } from '@angular/material/icon';
import { isAdminRole } from '../../../../shared/utils/jwt.util';

@Component({
  selector: 'app-header-bar',
  imports: [RouterModule, MatIconModule, NgIf, TranslatePipe],
  templateUrl: './header-bar.component.html',
  styleUrls: ['./header-bar.component.scss']
})
export class HeaderBarComponent implements OnInit {
  isAdmin = false;
  isDark = false;
  menuOpen = false;

  constructor(
    private router: Router,
    private authService: AuthService,
    public languageService: LanguageService
  ){
    this.isAdmin = isAdminRole();
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

  logout(){
    this.authService.logout();
  }
}
