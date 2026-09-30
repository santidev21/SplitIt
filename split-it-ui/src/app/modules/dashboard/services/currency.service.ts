import { Injectable, inject } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Currency } from '../../../models/currency.model';

@Injectable({
  providedIn: 'root',
})
export class CurrencyService {
  private http = inject(HttpClient);

  private readonly API_URL = `${environment.apiUrl}/currencies`;

  getCurrencies(): Observable<Currency[]> {
    return this.http.get<Currency[]>(`${this.API_URL}`);
  }
}
