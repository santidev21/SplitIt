import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors, HttpErrorResponse } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from '../modules/auth/services/auth.service';

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
  let authServiceSpy: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    routerSpy = jasmine.createSpyObj('Router', ['navigate']);
    authServiceSpy = jasmine.createSpyObj('AuthService', ['getToken', 'refreshSession', 'logout']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: routerSpy },
        { provide: AuthService, useValue: authServiceSpy }
      ]
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should add Authorization header when token present', () => {
    const token = makeToken();
    authServiceSpy.getToken.and.returnValue(token);
    http.get('/api/test').subscribe();
    const req = httpMock.expectOne('/api/test');
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${token}`);
    req.flush({});
  });

  it('should not add Authorization header when no token', () => {
    authServiceSpy.getToken.and.returnValue(null);
    http.get('/api/test').subscribe();
    const req = httpMock.expectOne('/api/test');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('should skip refresh/logout requests from interception', () => {
    authServiceSpy.getToken.and.returnValue(null);
    http.post('/api/auth/refresh', {}).subscribe();
    const req = httpMock.expectOne('/api/auth/refresh');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({ token: 'new' });
  });

  it('should try refresh on 401 and retry if successful', () => {
    const token = makeToken();
    authServiceSpy.getToken.and.returnValue(token);
    authServiceSpy.refreshSession.and.returnValue({ pipe: (op: any) => op } as any);

    http.get('/api/protected').subscribe({
      error: () => {}
    });
    const req = httpMock.expectOne('/api/protected');
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${token}`);
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    authServiceSpy.logout.and.stub();
  });

  it('should propagate non-401 errors without logout', () => {
    const token = makeToken();
    authServiceSpy.getToken.and.returnValue(token);
    http.get('/api/test').subscribe({ error: () => {} });
    const req = httpMock.expectOne('/api/test');
    req.flush('Server error', { status: 500, statusText: 'Server Error' });
    expect(authServiceSpy.logout).not.toHaveBeenCalled();
  });
});
