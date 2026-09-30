import { Injectable, inject } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Expense, ExpenseParticipant } from '../../../models/expense.model';
import { FullDebtSummaryDto } from '../../../models/debts-summary';

@Injectable({
  providedIn: 'root',
})
export class ExpenseService {
  private http = inject(HttpClient);

  private readonly API_URL = `${environment.apiUrl}/expenses`;

  addExpense(expenseData: {
    title: string;
    amount: number;
    date: number;
    groupId: number;
    note?: string;
    paidById: number;
    participants: ExpenseParticipant[];
  }): Observable<any> {
    const body = expenseData;
    return this.http.post(`${this.API_URL}/add`, body);
  }

  getGroupExpenses(groupId: number, showAll: boolean): Observable<Expense[]> {
    return this.http.get<Expense[]>(`${this.API_URL}/${groupId}/expenses`, {
      params: { showAll },
    });
  }

  getFullDebtSummary(groupId: number): Observable<FullDebtSummaryDto> {
    return this.http.get<FullDebtSummaryDto>(`${this.API_URL}/debt-summary?groupId=${groupId}`);
  }

  settleExpenseWithUser(body: {
    payerUserId: number;
    groupId: number;
    amount: number;
  }): Observable<any> {
    return this.http.post(`${this.API_URL}/settle`, body);
  }
}
