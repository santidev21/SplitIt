import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class AuthService {

  private API_URL = `${environment.apiUrl}/auth`;
  
  constructor(private http: HttpClient) { }

  login(email: string, password: string) : Observable<any>
  {
    return this.http.post(`${this.API_URL}/login`, { email, password })
  }

  register(userName: string, email: string, password: string) : Observable<any>
  {
    return this.http.post(`${this.API_URL}/register`, { name: userName, email, password })
  }
}
