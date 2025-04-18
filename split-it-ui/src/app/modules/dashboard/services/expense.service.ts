import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { User } from '../../../models/user.model';
import { Expense, ExpenseParticipant } from '../../../models/expense.model';

@Injectable({
  providedIn: 'root'
})
export class ExpenseService {
  private readonly API_URL = `${environment.apiUrl}/expenses`;

  constructor(private http: HttpClient) {}

  addExpense(expenseData: {
    title : string,
    amount : number,
    date : number,
    groupId : number,
    note? : string
    paidById : number,
    participants : ExpenseParticipant[]
  }): Observable<any> {
    const body = expenseData;
    return this.http.post(`${this.API_URL}/add`, body);
  }

  getGroupExpenses(groupId: number, showAll: boolean): Observable<Expense[]> {
    return this.http.get<Expense[]>(`${this.API_URL}/${groupId}/expenses`, {
      params: { showAll }
    });
  
  }

}
