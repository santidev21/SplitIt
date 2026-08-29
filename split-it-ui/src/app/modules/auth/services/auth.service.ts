import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { Router } from '@angular/router';

@Injectable({
  providedIn: 'root'
})
export class AuthService {

  private API_URL = `${environment.apiUrl}/auth`;
  
  constructor(private http: HttpClient,
    private router: Router,
  ) { }

  login(email: string, password: string) : Observable<any>
  {
    const body = { email, password };
    return this.http.post<{ token: string, userName: string, userId: number }>(`${this.API_URL}/login`, body)
    .pipe(
      tap(response => {
        localStorage.setItem('token', response.token);
        localStorage.setItem('userName', response.userName);
        localStorage.setItem('userId', response.userId.toString());
        this.router.navigate(['/dashboard/home']);
      })
    );
  }

  register(userName: string, email: string, password: string) : Observable<any>
  {
    const body = { name: userName, email, password };
    return this.http.post<{ token: string, userName: string, userId: number }>(`${this.API_URL}/register`, body)
      .pipe(
      tap(response => {
        localStorage.setItem('token', response.token);
        localStorage.setItem('userName', response.userName);
        localStorage.setItem('userId', response.userId.toString());
        this.router.navigate(['/dashboard/home']);
      })
    );
  }

  logout(): void {
    localStorage.removeItem('token');
    localStorage.removeItem('userName');
    localStorage.removeItem('userId');
    this.router.navigate(['auth/login']);
  }

  isAuthenticated(): boolean {
    const token = localStorage.getItem('token');
    if (!token) return false;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.exp && payload.exp > Math.floor(Date.now() / 1000);
    } catch { return false; }
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
}
