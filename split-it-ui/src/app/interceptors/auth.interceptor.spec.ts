import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { of, Subject } from 'rxjs';
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

  it('should refresh on 401 and retry with the new token', () => {
    const token = makeToken();
    const newToken = makeToken();
    authServiceSpy.getToken.and.returnValues(token, newToken);
    authServiceSpy.refreshSession.and.returnValue(of({ token: newToken }));

    http.get('/api/protected').subscribe({ error: () => {} });
    const first = httpMock.expectOne('/api/protected');
    expect(first.request.headers.get('Authorization')).toBe(`Bearer ${token}`);
    first.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    const retry = httpMock.expectOne('/api/protected');
    expect(retry.request.headers.get('Authorization')).toBe(`Bearer ${newToken}`);
    retry.flush({});
    expect(authServiceSpy.logout).not.toHaveBeenCalled();
  });

  it('should logout when the refresh fails', () => {
    const token = makeToken();
    authServiceSpy.getToken.and.returnValue(token);
    authServiceSpy.refreshSession.and.returnValue(of(null));

    http.get('/api/protected').subscribe({ error: () => {} });
    httpMock.expectOne('/api/protected').flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(authServiceSpy.logout).toHaveBeenCalled();
  });

  it('should share a single refresh across concurrent 401s', () => {
    const token = makeToken();
    authServiceSpy.getToken.and.returnValue(token);
    const refresh$ = new Subject<{ token: string } | null>();
    authServiceSpy.refreshSession.and.returnValue(refresh$.asObservable());

    http.get('/api/a').subscribe({ error: () => {} });
    http.get('/api/b').subscribe({ error: () => {} });
    httpMock.expectOne('/api/a').flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });
    httpMock.expectOne('/api/b').flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(authServiceSpy.refreshSession).toHaveBeenCalledTimes(1);

    refresh$.next({ token });
    refresh$.complete();

    httpMock.expectOne('/api/a').flush({});
    httpMock.expectOne('/api/b').flush({});
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
