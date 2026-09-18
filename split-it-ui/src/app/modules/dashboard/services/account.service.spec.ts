import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AccountService } from './account.service';
import { environment } from '../../../../environments/environment';

describe('AccountService', () => {
  let service: AccountService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AccountService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('exportData GETs /users/me/export', () => {
    service.exportData().subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/users/me/export`);
    expect(req.request.method).toBe('GET');
    req.flush({});
  });

  it('deleteAccount DELETEs /users/me with the password', () => {
    service.deleteAccount('secret').subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/users/me`);
    expect(req.request.method).toBe('DELETE');
    expect(req.request.body).toEqual({ password: 'secret' });
    req.flush({ message: 'ok' });
  });
});
