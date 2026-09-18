import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly API = `${environment.apiUrl}/users/me`;

  constructor(private http: HttpClient) {}

  exportData(): Observable<unknown> {
    return this.http.get(`${this.API}/export`);
  }

  deleteAccount(password: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(this.API, { body: { password } });
  }
}
