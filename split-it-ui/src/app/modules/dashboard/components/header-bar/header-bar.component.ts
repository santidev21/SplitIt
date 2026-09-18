import { Component, OnInit } from '@angular/core';
import { NgIf } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import Swal from 'sweetalert2';
import { AuthService } from '../../../auth/services/auth.service';
import { AccountService } from '../../services/account.service';
import { LanguageService } from '../../../../shared/services/language.service';
import { MatIconModule } from '@angular/material/icon';

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
  accountOpen = false;

  constructor(
    private router: Router,
    private authService: AuthService,
    private accountService: AccountService,
    private translate: TranslateService,
    public languageService: LanguageService
  ){
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

  toggleAccount(): void {
    this.accountOpen = !this.accountOpen;
  }

  exportData(): void {
    this.accountOpen = false;
    this.accountService.exportData().subscribe({
      next: (data) => {
        const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = 'splitit-my-data.json';
        anchor.click();
        URL.revokeObjectURL(url);
        Swal.fire({
          toast: true,
          position: 'center',
          icon: 'success',
          title: this.translate.instant('NOTIFICATIONS.EXPORT_READY'),
          showConfirmButton: false,
          timer: 2500,
        });
      }
    });
  }

  async deleteAccount(): Promise<void> {
    this.accountOpen = false;
    const result = await Swal.fire({
      title: this.translate.instant('NOTIFICATIONS.DELETE_CONFIRM_TITLE'),
      text: this.translate.instant('NOTIFICATIONS.DELETE_CONFIRM_TEXT'),
      input: 'password',
      inputPlaceholder: this.translate.instant('NOTIFICATIONS.DELETE_PASSWORD'),
      showCancelButton: true,
      confirmButtonText: this.translate.instant('COMMON.YES_DELETE'),
      cancelButtonText: this.translate.instant('COMMON.CANCEL'),
      confirmButtonColor: '#d33',
      icon: 'warning',
    });
    if (!result.isConfirmed || !result.value) return;

    this.accountService.deleteAccount(result.value).subscribe({
      next: () => {
        Swal.fire({
          toast: true,
          position: 'center',
          icon: 'success',
          title: this.translate.instant('NOTIFICATIONS.DELETE_SUCCESS'),
          showConfirmButton: false,
          timer: 2500,
        });
        this.authService.logout();
      }
    });
  }

  logout(){
    this.authService.logout();
  }
}
