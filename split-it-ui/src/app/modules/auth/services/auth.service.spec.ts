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
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('login should store token in memory and navigate', () => {
    const mockResp = { token: 'fake-jwt-token', userName: 'Alice', userId: 1 };
    service.login('alice@test.com', 'Pass123!').subscribe();
    const req = httpMock.expectOne(req => req.url.includes('/api/auth/login'));
    req.flush(mockResp);
    expect(service.getToken()).toBe('fake-jwt-token');
    expect(service.getUserName()).toBe('Alice');
    expect(service.getCurrentUserId()).toBe(1);
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/dashboard/home']);
  });

  it('register should store token in memory', () => {
    const mockResp = { token: 'reg-token', userName: 'Bob', userId: 2 };
    service.register('Bob', 'bob@test.com', 'Pass123!').subscribe();
    const req = httpMock.expectOne(req => req.url.includes('/api/auth/register'));
    req.flush(mockResp);
    expect(service.getToken()).toBe('reg-token');
  });

  it('logout should clear session and navigate to login', () => {
    service.logout();
    const req = httpMock.expectOne(req => req.url.includes('/api/auth/logout'));
    req.flush({});
    expect(service.getToken()).toBeNull();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['auth/login']);
  });

  it('isAuthenticated should return false when no token', () => {
    expect(service.isAuthenticated()).toBe(false);
  });

  it('isAuthenticated should validate exp', () => {
    function b64url(o: any) { return btoa(JSON.stringify(o)).replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_'); }
    const header = b64url({ alg: 'HS256', typ: 'JWT' });
    const validPayload = b64url({ sub: '1', exp: Math.floor(Date.now()/1000)+3600 });

    const mockResp = { token: `${header}.${validPayload}.sig`, userName: 'Alice', userId: 1 };
    service.login('alice@test.com', 'Pass123!').subscribe();
    httpMock.expectOne(req => req.url.includes('/api/auth/login')).flush(mockResp);
    expect(service.isAuthenticated()).toBe(true);
  });
});
