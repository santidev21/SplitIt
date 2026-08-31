import { Injectable, Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { environment } from '../../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, BehaviorSubject, catchError, of, switchMap, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { isTokenExpired } from '../../../shared/utils/jwt.util';

export interface AuthUser {
  token: string;
  userName: string;
  userId: number;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {

  private API_URL = `${environment.apiUrl}/auth`;
  private currentUserSubject = new BehaviorSubject<AuthUser | null>(null);
  currentUser$ = this.currentUserSubject.asObservable();
  private isBrowser: boolean;

  constructor(
    private http: HttpClient,
    private router: Router,
    @Inject(PLATFORM_ID) platformId: Object,
  ) {
    this.isBrowser = isPlatformBrowser(platformId);
  }

  login(email: string, password: string): Observable<{ token: string; userName: string; userId: number }> {
    const body = { email, password };
    return this.http.post<{ token: string; userName: string; userId: number }>(`${this.API_URL}/login`, body, { withCredentials: true })
      .pipe(
        tap(response => {
          this.setSession(response);
          localStorage.setItem('userName', response.userName);
          localStorage.setItem('userId', response.userId.toString());
          this.router.navigate(['/dashboard/home']);
        })
      );
  }

  register(userName: string, email: string, password: string): Observable<{ token: string; userName: string; userId: number }> {
    const body = { name: userName, email, password };
    return this.http.post<{ token: string; userName: string; userId: number }>(`${this.API_URL}/register`, body, { withCredentials: true })
      .pipe(
        tap(response => {
          this.setSession(response);
          localStorage.setItem('userName', response.userName);
          localStorage.setItem('userId', response.userId.toString());
          this.router.navigate(['/dashboard/home']);
        })
      );
  }

  loginWithGoogle(idToken: string): Observable<{ token: string; userName: string; userId: number }> {
    return this.http.post<{ token: string; userName: string; userId: number }>(`${this.API_URL}/google`, { idToken }, { withCredentials: true })
      .pipe(
        tap(response => {
          this.setSession(response);
          localStorage.setItem('userName', response.userName);
          localStorage.setItem('userId', response.userId.toString());
          this.router.navigate(['/dashboard/home']);
        })
      );
  }

  refreshSession(): Observable<{ token: string } | null> {
    if (!this.isBrowser) return of(null);
    return this.http.post<{ token: string }>(`${this.API_URL}/refresh`, {}, { withCredentials: true }).pipe(
      switchMap(response => {
        if (!response?.token || isTokenExpired(response.token)) {
          return throwError(() => new Error('Invalid refresh token'));
        }
        const current = this.currentUserSubject.value;
        if (current) {
          this.currentUserSubject.next({ ...current, token: response.token });
        } else {
          this.currentUserSubject.next({
            token: response.token,
            userName: localStorage.getItem('userName') || '',
            userId: parseInt(localStorage.getItem('userId') || '0', 10),
          });
        }
        return of(response);
      }),
      catchError(() => {
        this.clearSession();
        return of(null);
      })
    );
  }

  tryRestoreSession(): Observable<boolean> {
    if (!this.isBrowser) return of(false);
    return new Observable<boolean>(observer => {
      this.http.post<{ token: string }>(`${this.API_URL}/refresh`, {}, { withCredentials: true }).subscribe({
        next: (response) => {
          if (!response?.token || isTokenExpired(response.token)) {
            this.clearSession();
            observer.next(false);
            observer.complete();
            return;
          }
          this.currentUserSubject.next({
            token: response.token,
            userName: localStorage.getItem('userName') || '',
            userId: parseInt(localStorage.getItem('userId') || '0', 10),
          });
          observer.next(true);
          observer.complete();
        },
        error: () => {
          this.clearSession();
          observer.next(false);
          observer.complete();
        }
      });
    });
  }

  logout(): void {
    if (this.isBrowser) {
      this.http.post(`${this.API_URL}/logout`, {}, { withCredentials: true }).subscribe({
        error: () => {}
      });
    }
    this.clearSession();
    this.router.navigate(['auth/login']);
  }

  isAuthenticated(): boolean {
    const user = this.currentUserSubject.value;
    if (!user?.token) return false;
    try {
      const payload = JSON.parse(atob(user.token.split('.')[1]));
      return payload.exp && payload.exp > Math.floor(Date.now() / 1000);
    } catch { return false; }
  }

  isSessionRestoring(): boolean {
    return this.currentUserSubject.value === null && this.isBrowser;
  }

  getRoleFromToken(): string | null {
    const token = this.getToken();
    if (!token) return null;
    try {
      const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
      let base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      while (base64.length % 4 !== 0) base64 += '=';
      const bytes = Uint8Array.from(atob(base64), ch => ch.charCodeAt(0));
      const payload = JSON.parse(new TextDecoder().decode(bytes));
      return payload[ROLE_CLAIM] || payload['role'] || null;
    } catch { return null; }
  }

  isAdminRole(): boolean {
    const role = this.getRoleFromToken();
    return role === 'SuperAdmin' || role === 'Admin';
  }

  isSuperAdminRole(): boolean {
    return this.getRoleFromToken() === 'SuperAdmin';
  }

  getCurrentUserId(): number {
    return this.currentUserSubject.value?.userId || 0;
  }

  getUserName(): string | null {
    return this.currentUserSubject.value?.userName || localStorage.getItem('userName');
  }

  getToken(): string | null {
    return this.currentUserSubject.value?.token || null;
  }

  forgotPassword(email: string): Observable<any> {
    return this.http.post(`${this.API_URL}/forgot-password`, { email });
  }

  resetPassword(token: string, newPassword: string): Observable<any> {
    return this.http.post(`${this.API_URL}/reset-password`, { token, newPassword });
  }

  verifyResetCode(email: string, code: string, newPassword: string): Observable<any> {
    return this.http.post(`${this.API_URL}/verify-reset-code`, { email, code, newPassword });
  }

  private setSession(response: { token: string; userName: string; userId: number }): void {
    this.currentUserSubject.next({
      token: response.token,
      userName: response.userName,
      userId: response.userId,
    });
  }

  private clearSession(): void {
    this.currentUserSubject.next(null);
    if (this.isBrowser) {
      localStorage.removeItem('userName');
      localStorage.removeItem('userId');
    }
  }
}
