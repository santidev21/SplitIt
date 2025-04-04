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
        this.router.navigate(['/home']);
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
