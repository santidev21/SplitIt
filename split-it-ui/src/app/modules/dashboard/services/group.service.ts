import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { GroupMember } from '../../../models/group.model';

@Injectable({
  providedIn: 'root'
})
export class GroupService {

    private API_URL = `${environment.apiUrl}/groups`;
  
  constructor(private http: HttpClient) { }

  createGroup(groupData: {
    name: string;
    description: string;
    members: number[];
    allowToDeleteExpenses: boolean;
    currencyId: number;
  }): Observable<any> {
    const body = groupData;
    return this.http.post(`${this.API_URL}/create`, body);
  }

  getUserGroups(userId: number): Observable<any> {
    return this.http.get(`${this.API_URL}/user/${userId}`);
  }

  getGroupMembers(groupId: number): Observable<GroupMember[]> {
    return this.http.get<GroupMember[]>(`${this.API_URL}/${groupId}/members`);
  }
}
