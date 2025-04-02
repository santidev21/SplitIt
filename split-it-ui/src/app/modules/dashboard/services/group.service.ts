import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class GroupService {

    private API_URL = `${environment.apiUrl}/groups`;
  
  constructor(private http: HttpClient) { }

  createGroup(
    name: string,
    description: string,
    members: number[],
    allowToDeleteExpenses: boolean,
    currencyId: number
  ): Observable<any> {
    const body = { name, description, members, allowToDeleteExpenses, currencyId };
    return this.http.post(`${this.API_URL}/create`, body);
  }
}
