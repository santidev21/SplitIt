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
    return this.http.post<{ token: string; userName: string }>(`${this.API_URL}/login`, { email, password })
    .pipe(
      tap(response => {
        localStorage.setItem('token', response.token);
        localStorage.setItem('userName', response.userName);
        this.router.navigate(['/home']);
      })
    );
  }

  register(userName: string, email: string, password: string) : Observable<any>
  {
    return this.http.post<{ token: string, user: { name: string, email: string } }>(
      `${this.API_URL}/register`, { name: userName, email, password }
    ).pipe(
      tap(response => {
        localStorage.setItem('token', response.token);
        localStorage.setItem('userName', response.user.name);
        this.router.navigate(['/home']);
      })
    );
  }

  logout(): void {
    localStorage.removeItem('token');
    localStorage.removeItem('userName');
    this.router.navigate(['auth/login']);
  }
}
