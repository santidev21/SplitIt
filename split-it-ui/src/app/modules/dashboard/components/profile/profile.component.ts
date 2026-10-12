import { Component, OnInit, inject } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import Swal from 'sweetalert2';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { HeaderBarComponent } from '../header-bar/header-bar.component';
import { AccountService } from '../../services/account.service';
import { AuthService } from '../../../auth/services/auth.service';
import { NotificationService } from '../../../../shared/services/notification.service';

@Component({
  selector: 'app-profile',
  imports: [MATERIAL_IMPORTS, HeaderBarComponent, TranslatePipe],
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.scss'],
})
export class ProfileComponent implements OnInit {
  private accountService = inject(AccountService);
  private authService = inject(AuthService);
  private notifications = inject(NotificationService);
  private translate = inject(TranslateService);

  userName: string | null = null;

  ngOnInit(): void {
    this.userName = this.authService.getUserName();
  }

  /** Access/portability right: download a copy of the user's data (Ley 1581). */
  exportData(): void {
    this.accountService.exportData().subscribe({
      next: (data) => {
        const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = 'splitit-my-data.json';
        anchor.click();
        URL.revokeObjectURL(url);
        this.notifications.toast(this.translate.instant('NOTIFICATIONS.EXPORT_READY'), 'success');
      },
      error: () => {
        this.notifications.error(this.translate.instant('NOTIFICATIONS.EXPORT_FAILED'));
      },
    });
  }

  /** Suppression right: anonymize and deactivate the account (Ley 1581). */
  async deleteAccount(): Promise<void> {
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
        this.notifications.success(this.translate.instant('NOTIFICATIONS.DELETE_SUCCESS'));
        this.authService.logout();
      },
      error: () => {
        this.notifications.error(this.translate.instant('NOTIFICATIONS.DELETE_FAILED'));
      },
    });
  }
}
