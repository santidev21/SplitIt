import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface AdminStats {
  totalUsers: number;
  activeUsers: number;
  totalGroups: number;
  totalExpenses: number;
  totalPayments: number;
}

export interface UserAdmin {
  id: number;
  name: string;
  email: string;
  roleId: number;
  roleName: string;
  isActive: boolean;
  createdAt: string;
}

export interface UsersPage {
  items: UserAdmin[];
  total: number;
  page: number;
  pageSize: number;
}

export interface GroupAdmin {
  id: number;
  name: string;
  description: string;
  currencyId: number;
  memberCount: number;
  expenseCount: number;
  createdAt: string;
}

export interface PublicSettings {
  registrationEnabled: boolean;
  maxExpenseAmount: number;
}

export interface PasswordResetToken {
  id: number;
  token: string;
  userName: string;
  userEmail: string;
  expiresAt: string;
  createdAt: string;
}

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private readonly API_URL = `${environment.apiUrl}/admin`;

  constructor(private http: HttpClient) {}

  getStats(): Observable<AdminStats> {
    return this.http.get<AdminStats>(`${this.API_URL}/stats`);
  }

  getUsers(q = '', page = 1, pageSize = 20): Observable<UsersPage> {
    return this.http.get<UsersPage>(`${this.API_URL}/users`, {
      params: { q, page, pageSize }
    });
  }

  updateUserRole(userId: number, roleId: number): Observable<any> {
    return this.http.put(`${this.API_URL}/users/${userId}/role`, { roleId });
  }

  setUserActive(userId: number, isActive: boolean): Observable<any> {
    return this.http.put(`${this.API_URL}/users/${userId}/active`, { isActive });
  }

  getGroups(q = ''): Observable<GroupAdmin[]> {
    return this.http.get<GroupAdmin[]>(`${this.API_URL}/groups`, { params: { q } });
  }

  getSettings(): Observable<Record<string, string>> {
    return this.http.get<Record<string, string>>(`${this.API_URL}/settings`);
  }

  updateSettings(settings: Record<string, string>): Observable<any> {
    return this.http.put(`${this.API_URL}/settings`, settings);
  }

  createCurrency(name: string, symbol: string): Observable<any> {
    return this.http.post(`${this.API_URL}/currencies`, { name, symbol });
  }

  deleteCurrency(currencyId: number): Observable<any> {
    return this.http.delete(`${this.API_URL}/currencies/${currencyId}`);
  }

  getResetTokens(): Observable<PasswordResetToken[]> {
    return this.http.get<PasswordResetToken[]>(`${this.API_URL}/password-reset-tokens`);
  }

  deleteResetToken(tokenId: number): Observable<any> {
    return this.http.delete(`${this.API_URL}/password-reset-tokens/${tokenId}`);
  }
}
