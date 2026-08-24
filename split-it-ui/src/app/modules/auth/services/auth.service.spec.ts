import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let routerSpy: jasmine.SpyObj<Router>;

  beforeEach(() => {
    routerSpy = jasmine.createSpyObj('Router', ['navigate']);
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: routerSpy }
      ]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
    localStorage.clear();
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('login should store token and navigate to /dashboard/home', () => {
    const mockResp = { token: 'fake-jwt-token', userName: 'Alice', userId: 1 };
    service.login('alice@test.com', 'Pass123!').subscribe();
    const req = httpMock.expectOne(req => req.url.includes('/api/auth/login'));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'alice@test.com', password: 'Pass123!' });
    req.flush(mockResp);
    expect(localStorage.getItem('token')).toBe('fake-jwt-token');
    expect(localStorage.getItem('userName')).toBe('Alice');
    expect(localStorage.getItem('userId')).toBe('1');
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/dashboard/home']);
  });

  it('register should store token', () => {
    const mockResp = { token: 'reg-token', userName: 'Bob', userId: 2 };
    service.register('Bob', 'bob@test.com', 'Pass123!').subscribe();
    const req = httpMock.expectOne(req => req.url.includes('/api/auth/register'));
    expect(req.request.body).toEqual({ name: 'Bob', email: 'bob@test.com', password: 'Pass123!' });
    req.flush(mockResp);
    expect(localStorage.getItem('token')).toBe('reg-token');
  });

  it('logout should clear storage and navigate to login', () => {
    localStorage.setItem('token', 't');
    localStorage.setItem('userName', 'X');
    localStorage.setItem('userId', '1');
    service.logout();
    expect(localStorage.getItem('token')).toBeNull();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['auth/login']);
  });

  it('isAuthenticated should return false when no token', () => {
    expect(service.isAuthenticated()).toBe(false);
  });

  it('isAuthenticated should validate exp', () => {
    function b64url(o: any) { return btoa(JSON.stringify(o)).replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_'); }
    const header = b64url({ alg: 'HS256', typ: 'JWT' });
    const validPayload = b64url({ sub: '1', exp: Math.floor(Date.now()/1000)+3600 });
    localStorage.setItem('token', `${header}.${validPayload}.sig`);
    expect(service.isAuthenticated()).toBe(true);

    const expiredPayload = b64url({ sub: '1', exp: Math.floor(Date.now()/1000)-3600 });
    localStorage.setItem('token', `${header}.${expiredPayload}.sig`);
    expect(service.isAuthenticated()).toBe(false);
  });
});
