import { Component, OnInit } from '@angular/core';
import { MATERIAL_IMPORTS } from '../../../../../shared/material.imports';
import { HeaderBarComponent } from '../../../dashboard/components/header-bar/header-bar.component';
import { FormsModule } from '@angular/forms';
import { AdminService, AdminStats, UserAdmin, UsersPage, GroupAdmin, PasswordResetToken } from '../../services/admin.service';
import { NotificationService } from '../../../../shared/services/notification.service';
import { getCurrentUserId, isSuperAdminRole } from '../../../../shared/utils/jwt.util';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-admin-page',
  imports: [MATERIAL_IMPORTS, HeaderBarComponent, FormsModule, TranslatePipe],
  templateUrl: './admin-page.component.html',
  styleUrls: ['./admin-page.component.scss']
})
export class AdminPageComponent implements OnInit {
  selectedTab = 0;

  stats?: AdminStats;

  users: UserAdmin[] = [];
  usersTotal = 0;
  usersPage = 1;
  usersPageSize = 20;
  userSearch = '';
  readonly roles = [
    { id: 1, label: 'SuperAdmin' },
    { id: 2, label: 'Admin' },
    { id: 3, label: 'User' }
  ];
  isSuperAdmin = false;
  currentUserId = 0;

  groups: GroupAdmin[] = [];
  groupSearch = '';

  registrationEnabled = true;
  maxExpenseAmount = 1000000;
  newCurrencyName = '';
  newCurrencySymbol = '';

  resetTokens: PasswordResetToken[] = [];

  constructor(
    private adminService: AdminService,
    private notifications: NotificationService,
    private translate: TranslateService
  ) {}

  ngOnInit(): void {
    this.isSuperAdmin = isSuperAdminRole();
    this.currentUserId = getCurrentUserId();
    this.loadStats();
    this.loadUsers();
    this.loadGroups();
    this.loadSettings();
    if (this.isSuperAdmin) {
      this.loadResetTokens();
    }
  }

  loadStats(): void {
    this.adminService.getStats().subscribe({
      next: (stats) => { this.stats = stats; },
      error: () => {}
    });
  }

  loadUsers(): void {
    this.adminService.getUsers(this.userSearch, this.usersPage, this.usersPageSize).subscribe({
      next: (page: UsersPage) => {
        this.users = page.items;
        this.usersTotal = page.total;
      },
      error: () => {}
    });
  }

  onUserSearch(): void {
    this.usersPage = 1;
    this.loadUsers();
  }

  nextPage(): void {
    if (this.usersPage * this.usersPageSize < this.usersTotal) {
      this.usersPage++;
      this.loadUsers();
    }
  }

  prevPage(): void {
    if (this.usersPage > 1) {
      this.usersPage--;
      this.loadUsers();
    }
  }

  changeRole(user: UserAdmin, event: any): void {
    const roleId = Number(event.value);
    this.adminService.updateUserRole(user.id, roleId).subscribe({
      next: () => {
        user.roleId = roleId;
        user.roleName = this.roles.find(r => r.id === roleId)?.label ?? user.roleName;
        this.notifications.toast(this.translate.instant('NOTIFICATIONS.ROLE_UPDATED', { name: user.name }), 'success');
      },
      error: () => {}
    });
  }

  toggleActive(user: UserAdmin): void {
    const newValue = !user.isActive;
    this.adminService.setUserActive(user.id, newValue).subscribe({
      next: () => {
        user.isActive = newValue;
        this.notifications.toast(newValue ? this.translate.instant('NOTIFICATIONS.USER_ACTIVATED', { name: user.name }) : this.translate.instant('NOTIFICATIONS.USER_DEACTIVATED', { name: user.name }), 'success');
      },
      error: () => {}
    });
  }

  loadGroups(): void {
    this.adminService.getGroups(this.groupSearch).subscribe({
      next: (groups: GroupAdmin[]) => { this.groups = groups; },
      error: () => {}
    });
  }

  loadSettings(): void {
    this.adminService.getSettings().subscribe({
      next: (settings: Record<string, string>) => {
        this.registrationEnabled = (settings['RegistrationEnabled'] ?? 'true') === 'true';
        this.maxExpenseAmount = Number(settings['MaxExpenseAmount'] ?? 1000000);
      },
      error: () => {}
    });
  }

  saveSettings(): void {
    const settings: Record<string, string> = {
      RegistrationEnabled: String(this.registrationEnabled),
      MaxExpenseAmount: String(this.maxExpenseAmount)
    };
    this.adminService.updateSettings(settings).subscribe({
      next: () => this.notifications.success(this.translate.instant('NOTIFICATIONS.SETTINGS_UPDATED')),
      error: () => {}
    });
  }

  addCurrency(): void {
    const name = this.newCurrencyName.trim();
    const symbol = this.newCurrencySymbol.trim();
    if (!name || !symbol) {
      this.notifications.toast(this.translate.instant('NOTIFICATIONS.CURRENCY_NAME_SYMBOL_REQUIRED'), 'warning');
      return;
    }
    this.adminService.createCurrency(name, symbol).subscribe({
      next: () => {
        this.notifications.toast(this.translate.instant('NOTIFICATIONS.CURRENCY_CREATED', { name }), 'success');
        this.newCurrencyName = '';
        this.newCurrencySymbol = '';
      },
      error: () => {}
    });
  }

  loadResetTokens(): void {
    this.adminService.getResetTokens().subscribe({
      next: (tokens) => { this.resetTokens = tokens; },
      error: () => {}
    });
  }

  deleteResetToken(token: PasswordResetToken): void {
    this.adminService.deleteResetToken(token.id).subscribe({
      next: () => {
        this.resetTokens = this.resetTokens.filter(t => t.id !== token.id);
        this.notifications.toast(this.translate.instant('NOTIFICATIONS.TOKEN_DELETED'), 'success');
      },
      error: () => {}
    });
  }
}
