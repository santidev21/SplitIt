import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors, HttpErrorResponse } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import Swal from 'sweetalert2';
import { errorInterceptor, extractBackendMessage } from './error.interceptor';

describe('errorInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting()
      ]
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('shows a toast on server errors', () => {
    const fire = spyOn(Swal, 'fire');
    http.get('/api/test').subscribe({ error: () => {} });
    httpMock.expectOne('/api/test').flush('boom', { status: 500, statusText: 'Server Error' });
    expect(fire).toHaveBeenCalled();
  });

  it('does NOT toast a 401 on a regular API call (handled by auth interceptor)', () => {
    const fire = spyOn(Swal, 'fire');
    http.get('/api/test').subscribe({ error: () => {} });
    httpMock.expectOne('/api/test').flush('nope', { status: 401, statusText: 'Unauthorized' });
    expect(fire).not.toHaveBeenCalled();
  });

  it('toasts a 401 on an auth endpoint (e.g. wrong password)', () => {
    const fire = spyOn(Swal, 'fire');
    http.post('/api/auth/login', {}).subscribe({ error: () => {} });
    httpMock.expectOne('/api/auth/login').flush('nope', { status: 401, statusText: 'Unauthorized' });
    expect(fire).toHaveBeenCalled();
  });

  it('extracts validation messages', () => {
    const err = new HttpErrorResponse({ status: 400, error: { errors: { Name: ['Required'] } } });
    expect(extractBackendMessage(err)).toBe('Required');
  });
});
