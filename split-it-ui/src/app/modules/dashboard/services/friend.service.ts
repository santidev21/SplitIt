import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Friend {
  id: number;
  name: string;
  email: string;
}

export interface FriendRequest {
  friendshipId: number;
  userId: number;
  name: string;
  email: string;
  createdAt: string;
}

export interface FriendRequestsResponse {
  incoming: FriendRequest[];
  sent: FriendRequest[];
}

export interface SearchUser {
  id: number;
  name: string;
  email: string;
}

@Injectable({
  providedIn: 'root'
})
export class FriendService {
  private readonly API_URL = `${environment.apiUrl}/friends`;

  constructor(private http: HttpClient) {}

  getFriends(): Observable<Friend[]> {
    return this.http.get<Friend[]>(`${this.API_URL}`);
  }

  getRequests(): Observable<FriendRequestsResponse> {
    return this.http.get<FriendRequestsResponse>(`${this.API_URL}/requests`);
  }

  sendRequest(body: { userId?: number; email?: string }): Observable<any> {
    return this.http.post(`${this.API_URL}/request`, body);
  }

  respond(friendshipId: number, accept: boolean): Observable<any> {
    return this.http.post(`${this.API_URL}/${friendshipId}/respond`, { accept });
  }

  removeFriend(friendUserId: number): Observable<any> {
    return this.http.delete(`${this.API_URL}/${friendUserId}`);
  }

  search(q: string): Observable<SearchUser[]> {
    return this.http.get<SearchUser[]>(`${this.API_URL}/search`, { params: { q } });
  }
}
