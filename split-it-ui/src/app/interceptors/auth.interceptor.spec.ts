import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors, HttpErrorResponse } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
// fakeJwt not needed — inline impl below

// Inline fakeJwt for unit context
function b64url(obj: any) { return btoa(JSON.stringify(obj)).replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_'); }
function makeToken() {
  const header = { alg: 'HS256', typ: 'JWT' };
  const payload = { sub: '1', exp: Math.floor(Date.now()/1000)+3600 };
  return `${b64url(header)}.${b64url(payload)}.sig`;
}

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let routerSpy: jasmine.SpyObj<Router>;

  beforeEach(() => {
    routerSpy = jasmine.createSpyObj('Router', ['navigate']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: routerSpy }
      ]
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    localStorage.clear();
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('should add Authorization header when token present', () => {
    const token = makeToken();
    localStorage.setItem('token', token);
    http.get('/api/test').subscribe();
    const req = httpMock.expectOne('/api/test');
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${token}`);
    req.flush({});
  });

  it('should not add Authorization header when no token', () => {
    http.get('/api/test').subscribe();
    const req = httpMock.expectOne('/api/test');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('should clear storage and navigate to login on 401', () => {
    localStorage.setItem('token', makeToken());
    localStorage.setItem('userName', 'Alice');
    localStorage.setItem('userId', '1');
    http.get('/api/protected').subscribe({
      error: () => {}
    });
    const req = httpMock.expectOne('/api/protected');
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });
    expect(localStorage.getItem('token')).toBeNull();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/auth/login']);
  });

  it('should propagate non-401 errors without logout', () => {
    localStorage.setItem('token', makeToken());
    http.get('/api/test').subscribe({ error: () => {} });
    const req = httpMock.expectOne('/api/test');
    req.flush('Server error', { status: 500, statusText: 'Server Error' });
    expect(localStorage.getItem('token')).not.toBeNull();
    expect(routerSpy.navigate).not.toHaveBeenCalled();
  });
});
